using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using InventoryAPI.Data;
using InventoryAPI.Models;
using InventoryAPI.DTOs;
using InventoryAPI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryAPI.Controllers
{
    [Route("api/Devices")]
    [ApiController]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly InventoryDbContext _context;
        private readonly EncryptionService _encryptionService;
        private readonly ISyslogService _syslogService;

        public DevicesController(InventoryDbContext context, EncryptionService encryptionService, ISyslogService syslogService)
        {
            _context = context;
            _encryptionService = encryptionService;
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

        private bool IsGlobalAdmin()
        {
            var username = GetActiveUsername();
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            return username == "tayfun.kaydi" || role == "SuperAdmin" || role == "MainAdmin";
        }

        private bool HasAdminPrivileges()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            return IsGlobalAdmin() || role == "Operator" || role.Contains("Lider");
        }

        // --- 1. CİHAZLARI LİSTELEME ---
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetDevices()
        {
            var userDept = User.FindFirst("Department")?.Value ?? "";
            var username = GetActiveUsername();

            IQueryable<Device> query = _context.Devices;

            if (!IsGlobalAdmin())
            {
                query = query.Where(d => d.VisibleTo == userDept || d.VisibleTo == "Both");
            }

            var result = await query.Select(d => new
            {
                d.Id,
                d.Customer,
                d.Site, // EKLENDİ
                d.Vendor,
                d.Version, // EKLENDİ
                d.Name,
                d.Hostname,
                d.IpAddress,
                d.Gateway,
                d.Username,
                d.VisibleTo,
                d.SerialNumber,
                d.Description,
                d.IsActive,
                d.CreatedAt
            }).ToListAsync();

            return Ok(result);
        }

        // --- 2. YENİ CİHAZ EKLEME ---
        [HttpPost]
        public async Task<ActionResult<Device>> PostDevice(DeviceCreateDto dto)
        {
            if (!HasAdminPrivileges())
            {
                await _syslogService.SendLogAsync(GetActiveUsername(), "UnauthorizedAction", "CreateDevice", "İzleyici yetkisiyle cihaz ekleme denemesi engellendi!", "Warning", GetClientIp());
                return StatusCode(403, "Sisteme yeni cihaz ekleme yetkiniz bulunmuyor.");
            }

            var userDept = User.FindFirst("Department")?.Value ?? "";
            if (!IsGlobalAdmin() && dto.VisibleTo != "Both" && dto.VisibleTo != userDept)
            {
                await _syslogService.SendLogAsync(GetActiveUsername(), "UnauthorizedAction", "CreateDevice", "Başka departmana cihaz ekleme girişimi engellendi!", "Warning", GetClientIp());
                return StatusCode(403, "Sadece kendi departmanınıza veya ortak havuza cihaz ekleyebilirsiniz.");
            }

            // ŞAH MAT: Eğer Seri No boş bırakıldıysa, çakışmayı önlemek için otomatik benzersiz bir SN üret!
            string finalSerial = string.IsNullOrWhiteSpace(dto.SerialNumber) 
                ? $"AUTO-SN-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}" 
                : dto.SerialNumber;

            var newDevice = new Device
            {
                Customer = dto.Customer,
                Site = dto.Site, 
                Vendor = dto.Vendor,
                Version = dto.Version, 
                Name = dto.Name,
                Hostname = dto.Hostname,
                IpAddress = dto.IpAddress,
                Gateway = dto.Gateway,
                Username = dto.Username,
                VisibleTo = dto.VisibleTo,
                SerialNumber = finalSerial, // <-- BURASI GÜNCELLENDİ
                Description = dto.Description,
                EncryptedPassword = _encryptionService.Encrypt(dto.Password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Devices.Add(newDevice);
            await _context.SaveChangesAsync();

            await _syslogService.SendLogAsync(GetActiveUsername(), "CreateDevice", dto.Name, $"{dto.Vendor} marka '{dto.Name}' cihazı eklendi.", "Success", GetClientIp());

            return Ok(newDevice);
        }

        // --- 3. CİHAZ GÜNCELLEME ---
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDevice(int id, DeviceUpdateDto dto)
        {
            if (id != dto.Id) return BadRequest("ID eşleşmiyor.");

            if (!HasAdminPrivileges())
            {
                await _syslogService.SendLogAsync(GetActiveUsername(), "UnauthorizedAction", dto.Name, "İzleyicinin cihazı düzenleme girişimi engellendi!", "Critical", GetClientIp());
                return StatusCode(403, "Cihaz düzenleme yetkiniz yok.");
            }

            var device = await _context.Devices.FindAsync(id);
            if (device == null) return NotFound("Cihaz bulunamadı.");

            var userDept = User.FindFirst("Department")?.Value ?? "";

            if (!IsGlobalAdmin() && device.VisibleTo != userDept && device.VisibleTo != "Both")
            {
                await _syslogService.SendLogAsync(GetActiveUsername(), "UnauthorizedAction", device.Name, "Başka departmanın cihazını düzenleme denemesi engellendi!", "Warning", GetClientIp());
                return StatusCode(403, "Bu departmanın cihazını düzenleme yetkiniz yok.");
            }

            // ŞAH MAT 2: Eğer güncelleme sırasında Seri No silinirse veya boş bırakılırsa, patlamamak için üret!
            string finalSerial = string.IsNullOrWhiteSpace(dto.SerialNumber) 
                ? $"AUTO-SN-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}" 
                : dto.SerialNumber;

            device.Customer = dto.Customer;
            device.Site = dto.Site; 
            device.Vendor = dto.Vendor;
            device.Version = dto.Version; 
            device.Name = dto.Name;
            device.Hostname = dto.Hostname;
            device.IpAddress = dto.IpAddress;
            device.Gateway = dto.Gateway;
            device.Username = dto.Username;
            device.VisibleTo = dto.VisibleTo;
            device.SerialNumber = finalSerial; // <-- BURASI GÜNCELLENDİ
            device.Description = dto.Description;
            device.IsActive = dto.IsActive;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                device.EncryptedPassword = _encryptionService.Encrypt(dto.Password);
            }

            await _context.SaveChangesAsync();
            await _syslogService.SendLogAsync(GetActiveUsername(), "UpdateDevice", device.Name, $"'{device.Name}' bilgileri güncellendi.", "Success", GetClientIp());

            return Ok(device);
        }

        // --- 4. CİHAZ SİLME ---
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDevice(int id)
        {
            if (!HasAdminPrivileges())
            {
                await _syslogService.SendLogAsync(GetActiveUsername(), "UnauthorizedAction", $"DeviceID:{id}", "Yetkisiz silme denemesi engellendi!", "Critical", GetClientIp());
                return StatusCode(403, "Cihaz silme yetkiniz bulunmuyor.");
            }

            var device = await _context.Devices.FindAsync(id);
            if (device == null) return NotFound("Cihaz bulunamadı.");

            var userDept = User.FindFirst("Department")?.Value ?? "";

            if (!IsGlobalAdmin() && device.VisibleTo != userDept && device.VisibleTo != "Both")
            {
                await _syslogService.SendLogAsync(GetActiveUsername(), "UnauthorizedAction", device.Name, "Başka departmanın cihazını silme denemesi engellendi!", "Warning", GetClientIp());
                return StatusCode(403, "Bu departmanın cihazını silme yetkiniz yok.");
            }

            string logDetail = $"'{device.Name}' cihazı envanterden kalıcı olarak silindi.";
            _context.Devices.Remove(device);
            await _context.SaveChangesAsync();

            await _syslogService.SendLogAsync(GetActiveUsername(), "DeleteDevice", device.Name, logDetail, "Success", GetClientIp());

            return Ok(new { message = "Cihaz silindi." });
        }

        // --- 5. CİHAZ DEVRETME ---
        [HttpPut("{id}/transfer")]
        public async Task<IActionResult> TransferDevice(int id, [FromBody] TransferDto dto)
        {
            if (!HasAdminPrivileges()) return StatusCode(403, "Cihaz departmanını değiştirme yetkiniz yok.");

            var device = await _context.Devices.FindAsync(id);
            if (device == null) return NotFound();

            var userDept = User.FindFirst("Department")?.Value ?? "";

            if (!IsGlobalAdmin() && device.VisibleTo != userDept && device.VisibleTo != "Both")
            {
                return StatusCode(403, "Sadece yetkili olduğunuz departmanın cihazlarını devredebilirsiniz.");
            }

            string oldDept = device.VisibleTo;
            device.VisibleTo = dto.VisibleTo;

            await _context.SaveChangesAsync();

            await _syslogService.SendLogAsync(GetActiveUsername(), "TransferDevice", device.Name, $"'{device.Name}' cihazı {oldDept} departmanından {dto.VisibleTo} departmanına devredildi.", "Success", GetClientIp());

            return Ok(new { message = "Cihaz başarıyla devredildi." });
        }

        // --- 6. ŞİFREYİ ÇÖZÜP GETİRME ---
        [HttpGet("{id}/password")]
        public async Task<ActionResult> GetDevicePassword(int id)
        {
            try 
            {
                var device = await _context.Devices.FindAsync(id);
                if (device == null) return NotFound("Cihaz bulunamadı.");

                var userDept = User.FindFirst("Department")?.Value ?? "";

                if (!IsGlobalAdmin() && device.VisibleTo != userDept && device.VisibleTo != "Both")
                {
                    return StatusCode(403, "Bu cihazın şifresini görüntüleme yetkiniz yok.");
                }

                if (string.IsNullOrWhiteSpace(device.EncryptedPassword))
                {
                    return Ok(new { password = "ŞİFRE GİRİLMEMİŞ" });
                }

                string decryptedPassword;
                try
                {
                    decryptedPassword = _encryptionService.Decrypt(device.EncryptedPassword);
                }
                catch
                {
                    decryptedPassword = "ŞİFRELENMEMİŞ: " + device.EncryptedPassword;
                }

                try 
                {
                    await _syslogService.SendLogAsync(GetActiveUsername(), "ViewPassword", device.Name ?? "Bilinmeyen", $"'{device.Name}' cihazının şifresi görüntülendi.", "Warning", GetClientIp());
                } 
                catch { }

                return Ok(new { password = decryptedPassword });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Sunucuda beklenmeyen bir hata oluştu: " + ex.Message);
            }
        }
    }
}