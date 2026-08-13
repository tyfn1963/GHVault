using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; 
using System.Security.Claims;
using InventoryAPI.Data;
using InventoryAPI.Models;
using InventoryAPI.Services; 
using System.Threading.Tasks;
using System.DirectoryServices; 
using System.Collections.Generic;
using System;
using System.Linq;
using System.Runtime.Versioning; // CA1416 uyarısını susturmak için eklendi

namespace InventoryAPI.Controllers
{
    [SupportedOSPlatform("windows")] // CA1416 ÇÖZÜMÜ: Sadece Windows'ta çalışır
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "SuperAdmin, MainAdmin")]
    public class LdapSettingsController : ControllerBase
    {
        private readonly InventoryDbContext _context;
        private readonly ISyslogService _syslogService;

        public LdapSettingsController(InventoryDbContext context, ISyslogService syslogService)
        {
            _context = context;
            _syslogService = syslogService;
        }

        private string GetActiveUsername()
        {
            return User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "BilinmeyenKullanici";
        }

        private string GetClientIp() 
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        }

        // === 🛡️ GOD-MODE VE YETKİ KALKANI (ZIRHLI VERSİYON) ===
        private bool HasAdminPrivileges()
        {
            var username = GetActiveUsername();
            
            // ŞAH MAT: C#'ın token'dan rolü kaçırmaması için tüm isim varyasyonlarını kontrol ediyoruz!
            var role = User.FindFirst(ClaimTypes.Role)?.Value 
                    ?? User.FindFirst("role")?.Value 
                    ?? User.FindFirst("Role")?.Value 
                    ?? "";

            return username == "tayfun.kaydi" 
                || role == "SuperAdmin" 
                || role == "MainAdmin" 
                || role.Contains("Lider");
        }

        // --- 1. AYARLARI EKRANA GETİR (GET) ---
        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            if (!HasAdminPrivileges())
            {
                // CS4014 ÇÖZÜMÜ: Log metotlarının başına await ve GetClientIp eklendi
                await _syslogService.SendLogAsync(GetActiveUsername(), "UnauthorizedAction", "LdapSettings", "Yetkisiz LDAP ayarları görüntüleme denemesi!", "Warning", GetClientIp());
                return StatusCode(403, "LDAP ayarlarını görüntüleme yetkiniz yok.");
            }

            var settings = await _context.LdapSettings.FirstOrDefaultAsync();
            
            // 💥 EĞER VERİTABANI BOŞSA (NULL İSE) HATA FIRLATMA, BOŞ KUTULARI DÖN 💥
            if (settings == null) 
            {
                return Ok(new { 
                    ServerIp = "", 
                    Port = 389, 
                    CommonNameIdentifier = "sAMAccountName", 
                    DistinguishedName = "", 
                    BindType = "Simple", 
                    UserDn = "", 
                    SecureConnection = false,
                    Password = ""
                });
            }

            // 💥 EĞER VERİTABANINDA AYAR VARSA, DOLU KUTULARI (ŞİFREYİ MASKELİYEREK) DÖN 💥
            var safeSettings = new {
                settings.Id,
                settings.Name,
                settings.ServerIp,
                settings.Port,
                settings.CommonNameIdentifier,
                settings.DistinguishedName,
                settings.BindType,
                settings.UserDn,
                settings.SecureConnection,
                Password = "********" 
            };

            return Ok(safeSettings);
        }

        // --- 2. AYARLARI KAYDET / GÜNCELLE (PUT) ---
        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] LdapSetting updatedSettings)
        {
            if (!HasAdminPrivileges())
            {
                await _syslogService.SendLogAsync(GetActiveUsername(), "UnauthorizedAction", "LdapSettings", "Kritik AD ayarlarını değiştirme denemesi!", "Critical", GetClientIp());
                return StatusCode(403, "Active Directory ayarlarını değiştirme yetkiniz yok.");
            }

            var settings = await _context.LdapSettings.FirstOrDefaultAsync();
            string username = GetActiveUsername();
            
            if (settings == null)
            {
                _context.LdapSettings.Add(updatedSettings);
                await _syslogService.SendLogAsync(username, "CreateLdapSettings", updatedSettings.ServerIp ?? "Unknown", "Yeni AD entegrasyon ayarları kaydedildi.", "Info", GetClientIp());
            }
            else
            {
                settings.Name = updatedSettings.Name;
                settings.ServerIp = updatedSettings.ServerIp;
                settings.Port = updatedSettings.Port;
                settings.CommonNameIdentifier = updatedSettings.CommonNameIdentifier;
                settings.DistinguishedName = updatedSettings.DistinguishedName;
                settings.BindType = updatedSettings.BindType;
                settings.UserDn = updatedSettings.UserDn;
                settings.SecureConnection = updatedSettings.SecureConnection;

                if (!string.IsNullOrEmpty(updatedSettings.Password) && updatedSettings.Password != "********")
                {
                    settings.Password = updatedSettings.Password;
                }

                await _syslogService.SendLogAsync(username, "UpdateLdapSettings", updatedSettings.ServerIp ?? "Unknown", "AD entegrasyon ayarları güncellendi.", "Warning", GetClientIp());
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "AD Entegrasyon ayarları başarıyla güncellendi!" });
        }

        // --- 3. AD SUNUCUSUNA BAĞLAN VE TÜM PERSONELİ ÇEK (GET) ---
        [HttpGet("ad-users")]
        public async Task<IActionResult> GetAdUsers()
        {
            if (!HasAdminPrivileges())
            {
                await _syslogService.SendLogAsync(GetActiveUsername(), "UnauthorizedAction", "AdUsers", "Tüm AD kullanıcı listesini çekme denemesi!", "Critical", GetClientIp());
                return StatusCode(403, "Active Directory kullanıcı listesine erişim yetkiniz yok.");
            }

            var settings = await _context.LdapSettings.FirstOrDefaultAsync();
            if (settings == null) return BadRequest("LDAP ayarları bulunamadı. Lütfen önce ayarları kaydedin.");

            var adUsers = new List<object>();
            string username = GetActiveUsername();
            string identifier = settings.CommonNameIdentifier ?? "sAMAccountName";

            try
            {
                string path = $"LDAP://{settings.ServerIp}:{settings.Port}/{settings.DistinguishedName}";
                
                using (DirectoryEntry entry = new DirectoryEntry(path, settings.UserDn, settings.Password))
                {
                    using (DirectorySearcher searcher = new DirectorySearcher(entry))
                    {
                        searcher.Filter = "(&(objectCategory=person)(objectClass=user))";
                        
                        searcher.PropertiesToLoad.Add("givenName"); 
                        searcher.PropertiesToLoad.Add("sn"); 
                        searcher.PropertiesToLoad.Add(identifier); 
                        searcher.PropertiesToLoad.Add("department"); 
                        
                        searcher.PageSize = 1000;
                        SearchResultCollection results = searcher.FindAll();

                        foreach (SearchResult result in results)
                        {
                            // CS8600 ÇÖZÜMÜ: null olabilecek verileri güvenli hale getirdik
                            string fName = result.Properties.Contains("givenName") && result.Properties["givenName"].Count > 0 ? result.Properties["givenName"][0]?.ToString() ?? "" : "";
                            string lName = result.Properties.Contains("sn") && result.Properties["sn"].Count > 0 ? result.Properties["sn"][0]?.ToString() ?? "" : "";
                            string uName = result.Properties.Contains(identifier) && result.Properties[identifier].Count > 0 ? result.Properties[identifier][0]?.ToString() ?? "" : "";
                            string dept = result.Properties.Contains("department") && result.Properties["department"].Count > 0 ? result.Properties["department"][0]?.ToString() ?? "Belirtilmemiş" : "Belirtilmemiş";

                            if (!string.IsNullOrEmpty(uName))
                            {
                                adUsers.Add(new { 
                                    FirstName = fName, 
                                    LastName = lName, 
                                    Username = uName, 
                                    Department = dept 
                                });
                            }
                        }
                    }
                }

                await _syslogService.SendLogAsync(username, "PullAdUsers", settings.ServerIp ?? "Unknown", $"{adUsers.Count} adet AD kullanıcısı listelendi.", "Info", GetClientIp());
                return Ok(adUsers);
            }
            catch (Exception ex)
            {
                // CS0168 ÇÖZÜMÜ: 'ex' değişkenini SIEM loguna ekledik. Uyarı sustu, log zenginleşti!
                await _syslogService.SendLogAsync(username, "AdConnectionFailed", settings.ServerIp ?? "Unknown", $"AD bağlantı hatası: {ex.Message}", "Failed", GetClientIp());
                
                // Kullanıcıya giden mesaj hala %100 güvenli, sistem detayları gizli.
                return StatusCode(500, "AD Sunucusuna bağlanılamadı. Ayarları kontrol edin.");
            }
        }
    }
}