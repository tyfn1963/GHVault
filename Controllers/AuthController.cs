using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.DirectoryServices.AccountManagement;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using InventoryAPI.Data;
using InventoryAPI.DTOs;
using InventoryAPI.Models;
using InventoryAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace InventoryAPI.Controllers
{
    [SupportedOSPlatform("windows")]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly InventoryDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ISyslogService _syslogService;

        // === AKTİF OTURUMLAR VE KICK SİSTEMİ İÇİN STATİK HAFIZA ===
        public static ConcurrentDictionary<string, DateTime> ActiveSessions = new ConcurrentDictionary<string, DateTime>();
        public static ConcurrentDictionary<string, bool> KickedUsers = new ConcurrentDictionary<string, bool>();

        public AuthController(InventoryDbContext context, IConfiguration configuration, ISyslogService syslogService)
        {
            _context = context;
            _configuration = configuration;
            _syslogService = syslogService;
        }

        // --- 0. YARDIMCI METOT: USERNAME TEMİZLEYİCİ (DOMAIN VE EMAIL KORUMASI) ---
        private string CleanUsername(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (input.Contains("\\")) return input.Split('\\')[1]; // DOMAIN\user formatı koruması
            if (input.Contains("@")) return input.Split('@')[0];   // user@domain.com formatı koruması
            return input;
        }

        // --- 1. YENİ YEREL KAYIT (LOCAL REGISTER) MOTORU 🛡️ ---
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] LocalRegisterDto request)
        {
            string cleanUsername = CleanUsername(request.Username);

            if (await _context.Users.AnyAsync(u => u.Username == cleanUsername))
                return BadRequest("Bu kullanıcı adı sistemde zaten mevcut.");

            var newUser = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Department = request.Department,
                Username = cleanUsername,
                PasswordHash = request.Password,
                Role = "Izleyici", // Yeni varsayılan en alt rol
                IsApproved = false
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            await _syslogService.SendLogAsync(cleanUsername, "UserRegistered", "LocalAuth", "Yeni kullanıcı kayıt oldu, onay bekliyor.", "Info", clientIp);

            return Ok(new { message = "Kayıt işleminiz başarıyla alındı. Kurucu onayının ardından giriş yapabilirsiniz." });
        }


        // --- 2. HYBRID LOGIN MOTORU (AD + LOCAL) ---
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                return BadRequest("Kullanıcı adı ve şifre boş olamaz.");

            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            string cleanUsername = CleanUsername(request.Username);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == cleanUsername);
            bool isAuthenticated = false;

            // ADIM 1: SİSTEM KURUCUSUNA ÖZEL ACİL DURUM KAPISI
            if (request.Username == "tayfun.kaydi" && request.Password == "KURUCU_HESAP")
            {
                isAuthenticated = true;
            }
            // ADIM 2: YEREL KİMLİK DOĞRULAMA 
            else if (user != null && user.PasswordHash != "AD_MANAGED")
            {
                if (user.PasswordHash == request.Password)
                {
                    isAuthenticated = true;
                }
            }
            // ADIM 3: ACTIVE DIRECTORY DOĞRULAMASI
            else
            {
                var ldapSettings = await _context.LdapSettings.FirstOrDefaultAsync();
                if (ldapSettings == null)
                {
                    await _syslogService.SendLogAsync(request.Username, "LoginFailed", "System", "AD Ayarları bulunamadı.", "Error", clientIp);
                    return StatusCode(500, "Sistem Hatası: Active Directory entegrasyon ayarları bulunamadı.");
                }

                try
                {
                    using (PrincipalContext context = new PrincipalContext(ContextType.Domain, ldapSettings.ServerIp))
                    {
                        isAuthenticated = context.ValidateCredentials(request.Username, request.Password);
                    }
                }
                catch (Exception ex)
                {
                    await _syslogService.SendLogAsync(request.Username, "LoginFailed", "ActiveDirectory", $"AD bağlantı hatası: {ex.Message}", "Failed", clientIp);
                    return StatusCode(500, "AD Sunucusuna ulaşılamadı. Lütfen IT ekibiyle iletişime geçin.");
                }
            }

            // KAPI KONTROLLERİ
            if (!isAuthenticated)
            {
                await _syslogService.SendLogAsync(request.Username, "LoginFailed", "System", "Geçersiz şifre veya kullanıcı adı.", "Warning", clientIp);
                return Unauthorized("Kullanıcı adı veya şifre hatalı.");
            }

            if (user == null)
            {
                await _syslogService.SendLogAsync(cleanUsername, "LoginFailed", "System", "Kullanıcının sistemde kaydı yok.", "Warning", clientIp);
                return Unauthorized("Şifreniz doğru ancak GHVault sisteminde henüz kaydınız/yetkiniz bulunmuyor.");
            }

            if (!user.IsApproved)
            {
                await _syslogService.SendLogAsync(cleanUsername, "LoginFailed", "System", "Onaylanmamış hesapla giriş denemesi.", "Warning", clientIp);
                return StatusCode(403, "Hesabınız henüz kurucu tarafından onaylanmamıştır. Lütfen bekleyin.");
            }

            // ADIM 4: İÇERİ AL VE TOKEN ÜRET
            var token = GenerateJwtToken(user);
            await _syslogService.SendLogAsync(user.Username, "LoginSuccess", "System", "Sisteme başarılı giriş yaptı.", "Success", clientIp);

            // AKTİF OTURUMU RAM'E KAYDET VE KICK DURUMUNU SIFIRLA
            ActiveSessions[user.Username] = DateTime.Now;
            KickedUsers[user.Username] = false;

            return Ok(new { token });
        }

        // --- 3. AD PERSONELİ YETKİLENDİRME ---
        [Authorize(Roles = "SuperAdmin, Operator, MainAdmin")]
        [HttpPost("register-staff")]
        public async Task<IActionResult> RegisterStaff([FromBody] UserRegisterDto request)
        {
            string cleanUsername = CleanUsername(request.Username);
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == cleanUsername);

            if (existingUser != null)
                return BadRequest("Bu personel zaten sistemde yetkili.");

            var newUser = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Username = cleanUsername,
                PasswordHash = "AD_MANAGED",
                Department = request.Department,
                Role = string.IsNullOrWhiteSpace(request.Role) ? "Izleyici" : request.Role.Trim(),
                IsApproved = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            string currentUsername = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

            await _syslogService.SendLogAsync(currentUsername, "UserAuthorized", "System", $"{cleanUsername} adlı personeli AD'den sisteme ({request.Role}) olarak ekledi.", "Success", clientIp);

            return Ok(new { message = "Personel sisteme başarıyla yetkilendirildi." });
        }

        // --- 4. PERSONEL LİSTESİNİ ÇEK ---
        [Authorize]
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new { u.Username, u.FirstName, u.LastName, u.Department, u.Role, u.IsApproved })
                .ToListAsync();

            return Ok(users);
        }

        // --- 5. BEKLEYEN ONAYLAR ---
        [Authorize(Roles = "SuperAdmin, MainAdmin")]
        [HttpGet("pending-users")]
        public async Task<IActionResult> GetPendingUsers()
        {
            var pending = await _context.Users
                .Where(u => !u.IsApproved)
                .Select(u => new { u.Username, u.FirstName, u.LastName, u.Department, u.Role })
                .ToListAsync();

            return Ok(pending);
        }

        // --- 6. KULLANICI ONAYLAMA ---
        [Authorize(Roles = "SuperAdmin, MainAdmin")]
        [HttpPut("approve-user/{username}")]
        public async Task<IActionResult> ApproveUser(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            user.IsApproved = true;
            await _context.SaveChangesAsync();

            string adminUser = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

            await _syslogService.SendLogAsync(adminUser, "UserApproved", "System", $"{username} kullanıcısının erişimi onaylandı.", "Success", clientIp);

            return Ok(new { message = "Kullanıcı erişimi onaylandı." });
        }

        // --- 7. KULLANICI REDDETME / SİLME ---
        [Authorize(Roles = "SuperAdmin, MainAdmin")]
        [HttpDelete("reject-user/{username}")]
        public async Task<IActionResult> RejectUser(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            string adminUser = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

            await _syslogService.SendLogAsync(adminUser, "UserRejected", "System", $"{username} kullanıcısının hesabı reddedildi/silindi.", "Warning", clientIp);

            return Ok(new { message = "Kullanıcı sistemden silindi." });
        }

        // --- 8. YETKİ DEVRETME (SÜPER ADMIN TRANSFERİ) ---
        [Authorize(Roles = "SuperAdmin, MainAdmin")]
        [HttpPost("transfer-role/{targetUsername}")]
        public async Task<IActionResult> TransferRole(string targetUsername)
        {
            var currentUsername = User.FindFirst(ClaimTypes.Name)?.Value;
            if (currentUsername == null) return Unauthorized();

            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);
            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == targetUsername);

            if (currentUser == null) return Unauthorized("Oturumunuz geçersiz veya kullanıcı bulunamadı.");
            if (targetUser == null) return NotFound("Hedef kullanıcı bulunamadı.");
            if (currentUser == targetUser) return BadRequest("Yetkiyi kendinize devredemezsiniz.");

            targetUser.Role = "SuperAdmin";
            currentUser.Role = "Izleyici";

            await _context.SaveChangesAsync();

            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            await _syslogService.SendLogAsync(currentUsername, "RoleTransferred", "System", $"Super Admin yetkisi {targetUsername} kullanıcısına devredildi.", "Critical", clientIp);

            return Ok(new { message = "Yetki başarıyla devredildi. Lütfen tekrar giriş yapın." });
        }

        // --- 9. ROL VE ŞİFRE GÜNCELLEME (KOMBİNE) SADECE SÜPER ADMIN ---
        [Authorize(Roles = "SuperAdmin, MainAdmin")]
        [HttpPut("update-role/{username}")]
        public async Task<IActionResult> UpdateRole(string username, [FromBody] UpdateRoleDto request)
        {
            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (targetUser == null)
                return NotFound("Kullanıcı sistemde bulunamadı.");

            string oldRole = targetUser.Role;
            targetUser.Role = string.IsNullOrWhiteSpace(request.NewRole) ? "Izleyici" : request.NewRole.Trim();
            string logMessage = $"'{username}' adlı personelin rolü '{oldRole}' seviyesinden '{request.NewRole}' seviyesine güncellendi.";

            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                if (targetUser.PasswordHash == "AD_MANAGED")
                    return BadRequest("Bu hesap Active Directory'ye bağlı! Rolünü güncelleyebilirsiniz ancak şifresini değiştiremezsiniz.");

                targetUser.PasswordHash = request.NewPassword;
                logMessage += " Ayrıca şifresi sıfırlandı.";
            }

            await _context.SaveChangesAsync();

            string currentUsername = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

            await _syslogService.SendLogAsync(currentUsername, "UserUpdated", "System", logMessage, "Info", clientIp);

            return Ok(new { message = "İşlem başarıyla tamamlandı!" });
        }

        // --- 10. SÜPER ADMIN İÇİN DİREKT YEREL KAYIT (ONAYSIZ) ---
        [Authorize(Roles = "SuperAdmin, MainAdmin")]
        [HttpPost("admin-register")]
        public async Task<IActionResult> AdminRegister([FromBody] AdminRegisterDto request)
        {
            string cleanUsername = CleanUsername(request.Username);

            if (await _context.Users.AnyAsync(u => u.Username == cleanUsername))
                return BadRequest("Kullanıcı adı sistemde zaten mevcut.");

            var newUser = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Department = request.Department,
                Username = cleanUsername,
                PasswordHash = request.Password,
                Role = string.IsNullOrWhiteSpace(request.Role) ? "Izleyici" : request.Role.Trim(),
                IsApproved = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            string adminUser = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

            await _syslogService.SendLogAsync(adminUser, "UserRegisteredAdmin", "System", $"Super Admin '{cleanUsername}' adlı personeli direkt oluşturdu ve onayladı.", "Success", clientIp);

            return Ok(new { message = "Yerel personel başarıyla oluşturuldu ve anında yetkilendirildi!" });
        }

        // --- 11. AKTİF (ONLİNE) KULLANICILARI GETİR ---
        [HttpGet("online-users")]
        [Authorize]
        public async Task<IActionResult> GetOnlineUsers()
        {
            var onlineUsers = new List<object>();
            var now = DateTime.Now;

            var activeUsernames = ActiveSessions
                .Where(kvp => (now - kvp.Value).TotalHours < 24 && (!KickedUsers.TryGetValue(kvp.Key, out bool isKicked) || !isKicked))
                .Select(kvp => kvp.Key)
                .ToList();

            var usersFromDb = await _context.Users
                .Where(u => activeUsernames.Contains(u.Username))
                .ToDictionaryAsync(u => u.Username, u => u.Role);

            foreach (var username in activeUsernames)
            {
                var sessionTime = ActiveSessions[username];
                var duration = now - sessionTime;

                string formattedDuration = duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours} sa {duration.Minutes} dk"
                    : $"{duration.Minutes} dk";

                string role = usersFromDb.ContainsKey(username) ? usersFromDb[username] : "Bilinmiyor";

                onlineUsers.Add(new
                {
                    username = username,
                    role = role,
                    sessionDuration = formattedDuration
                });
            }

            return Ok(onlineUsers);
        }

        // --- 12. KULLANICIYI SİSTEMDEN AT (FORCE LOGOUT) ---
        [HttpPost("force-logout/{username}")]
        [Authorize(Roles = "SuperAdmin, MainAdmin")]
        public IActionResult ForceLogoutUser(string username)
        {
            if (string.IsNullOrEmpty(username)) return BadRequest("Geçersiz kullanıcı.");

            KickedUsers[username] = true;
            ActiveSessions.TryRemove(username, out _);

            string currentUsername = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            _syslogService.SendLogAsync(currentUsername, "UserKicked", "System", $"Süper Admin tarafından {username} kullanıcısının oturumu zorla sonlandırıldı.", "Warning", clientIp);

            return Ok(new { message = "Kullanıcı sistemden başarıyla atıldı." });
        }

        // --- ŞAH MAT: ZIRHLI TOKEN ÜRETİCİ ---
        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration.GetSection("Jwt:Key").Value;
            var jwtIssuer = _configuration.GetSection("Jwt:Issuer").Value;

            if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer))
                throw new Exception("JWT Key veya Issuer ayarı eksik!");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // GİZLİ BOŞLUKLARI TRAŞLA (.Trim()) - 403 HATALARININ KÖKÜNÜ KAZIYORUZ
            string safeRole = string.IsNullOrWhiteSpace(user.Role) ? "Izleyici" : user.Role.Trim();

            var claims = new[]
{
    new Claim("sub", user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // Standart
                new Claim(ClaimTypes.Name, user.Username), // Standart
                new Claim(ClaimTypes.Role, safeRole), // Standart Rol! Başka 'role' eklemeye gerek yok.
                new Claim("FirstName", user.FirstName ?? ""),
                new Claim("LastName", user.LastName ?? ""),
                new Claim("Department", user.Department ?? "")
};

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtIssuer,
                claims: claims,
                expires: DateTime.Now.AddHours(8),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class UpdateRoleDto
    {
        public string NewRole { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AdminRegisterDto
    {
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}