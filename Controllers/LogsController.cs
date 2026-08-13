using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using InventoryAPI.Data;
using InventoryAPI.Services;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace InventoryAPI.Controllers
{

    [Route("api/[controller]")] // veya "api/logs" frontend ne atıyorsa
    [ApiController]
    [Authorize(Roles = "SuperAdmin, MainAdmin, Operator")] // BURADA SUPERADMIN ŞART
    public class LogsController : ControllerBase
    {
        private readonly InventoryDbContext _context;
        private readonly ISyslogService _syslogService;

        public LogsController(InventoryDbContext context, ISyslogService syslogService)
        {
            _context = context;
            _syslogService = syslogService;
        }

        [HttpGet("check-my-role")]
        [Authorize]
        public IActionResult CheckMyRole()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            return Ok(new { YourRoleInToken = role });
        }

        private string GetActiveUsername()
        {
            return User.Identity?.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "BilinmeyenKullanici";
        }

        private string GetClientIp()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        }

        // === 🛡️ GOD-MODE VE YETKİ KALKANI (DÜZELTİLDİ) ===
        // === 🛡️ GOD-MODE VE YETKİ KALKANI (ZIRHLI VERSİYON) ===
        private bool HasAdminPrivileges()
        {
            // İsim ve Rolü standart yoldan çekiyoruz
            var username = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

            // İŞTE ÇÖZÜM: Tayfun VEYA SuperAdmin VEYA MainAdmin VEYA Lider
            return username == "tayfun.kaydi" 
                || role == "SuperAdmin" 
                || role == "MainAdmin" 
                || role.Contains("Lider");
        }

        // --- 1. LOGLARI EKRANDA LİSTELE ---
        [HttpGet]
        public async Task<ActionResult> GetLogs()
        {
            if (!HasAdminPrivileges())
            {
                await _syslogService.SendLogAsync(GetActiveUsername(), "UnauthorizedAction", "ViewLogs", "Yetkisiz log görüntüleme denemesi!", "Warning", GetClientIp());
                return StatusCode(403, "Güvenlik loglarını görme yetkiniz yok.");
            }

            var logs = await (from log in _context.AuditLogs
                              join user in _context.Users on log.UserId equals user.Id into userGroup
                              from u in userGroup.DefaultIfEmpty()
                              orderby log.ActionDate descending
                              select new
                              {
                                  log.Id,
                                  Username = u != null ? u.Username : "ActiveDirectory_Kullanicisi",
                                  log.Action,
                                  log.Details,
                                  log.ActionDate
                              }).Take(50).ToListAsync();

            return Ok(logs);
        }

        // --- 2. SYSLOG FORMATINDA DIŞA AKTARMA (İNDİRME) ---
        [HttpGet("export/syslog")]
        public async Task<IActionResult> ExportSyslog([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            if (!HasAdminPrivileges())
            {
                await _syslogService.SendLogAsync(GetActiveUsername(), "DataExfiltrationAttempt", "ExportLogs", "Yetkisiz tüm logları indirme girişimi!", "Critical", GetClientIp());
                return StatusCode(403, "Sistem loglarını dışa aktarma yetkiniz bulunmuyor.");
            }

            var query = _context.AuditLogs.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(l => l.ActionDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(l => l.ActionDate <= endDate.Value);
            }

            var logs = await query.OrderByDescending(l => l.ActionDate).ToListAsync();
            var sb = new System.Text.StringBuilder();

            foreach (var log in logs)
            {
                string timestamp = log.ActionDate.ToString("MMM dd HH:mm:ss", new System.Globalization.CultureInfo("en-US"));
                string syslogLine = $"<14>{timestamp} GH-Envanter-Sunucusu InventoryAPI: [User ID: {log.UserId}] {log.Action} - {log.Details}";
                sb.AppendLine(syslogLine);
            }

            string currentUsername = GetActiveUsername();
            await _syslogService.SendLogAsync(currentUsername, "DataExport", "SyslogFile", $"{logs.Count} adet log dosyası olarak bilgisayara indirildi.", "Warning", GetClientIp());

            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            string fileName = $"GH_Envanter_Loglar_{DateTime.Now:yyyyMMdd_HHmm}.syslog";
            return File(fileBytes, "application/octet-stream", fileName);
        }
    }
}