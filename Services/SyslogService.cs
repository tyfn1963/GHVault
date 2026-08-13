using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; 
using Microsoft.Extensions.Configuration;
using InventoryAPI.Data;
using InventoryAPI.Models;

namespace InventoryAPI.Services
{
    public interface ISyslogService
    {
        // ASIL MOTOR: 6 Parametreli detaylı efsane yapı
        Task SendLogAsync(string username, string action, string target, string details, string status = "Success", string clientIp = "0.0.0.0");
        
        // İSVİÇRE ÇAKISI KÖPRÜ: 4 veya 5 parametre gönderen diğer Controller'ları hata vermeden yakalar!
        Task SendLog(string p1, string p2, string p3, string p4, string p5 = "Info", string p6 = "0.0.0.0");
    }

    public class SyslogService : ISyslogService, IDisposable
    {
        private readonly string _syslogIp;
        private readonly int _syslogPort;
        private readonly UdpClient _udpClient;
        private readonly InventoryDbContext _context;

        public SyslogService(IConfiguration config, InventoryDbContext context)
        {
            // OPTİMİZASYON 1: appsettings.json dosyasındaki isimlerle BİREBİR eşleştirildi!
            _syslogIp = config["SyslogSettings:ServerIp"] ?? "127.0.0.1"; 
            
            if (!int.TryParse(config["SyslogSettings:Port"], out _syslogPort))
            {
                _syslogPort = 514;
            }

            _udpClient = new UdpClient();
            _context = context;
        }

        // --- KÖPRÜ METOT ---
        public async Task SendLog(string p1, string p2, string p3, string p4, string p5 = "Info", string p6 = "0.0.0.0")
        {
            await SendLogAsync(p1, p2, p3, p4, p5, p6);
        }

        // --- ASIL LOG MOTORU ---
        public async Task SendLogAsync(string username, string action, string target, string details, string status = "Success", string clientIp = "0.0.0.0")
        {
            // 1. ADIM: SQL VERİTABANINA YAZ
            try 
            {
                var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

                var auditLog = new AuditLog 
                {
                    ActionDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time")),
                    // OPTİMİZASYON 2: AuditLog modelindeki int? (Nullable) zırhımızla tam uyumlu hale getirildi
                    UserId = dbUser?.Id, 
                    Action = $"{action} - {target}", 
                    Details = $"Durum: {status} | Detay: {details}"
                };
                
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[UYARI - SQL HATASI] Log veritabanına yazılamadı: {ex.Message}\n");
            }

            // 2. ADIM: SYSLOG MOTORUNU ÇALIŞTIR (QRadar İçin LEEF Formatı)
            try
            {
                string safeDetails = details?.Replace("\n", " ").Replace("\r", "").Replace("\"", "'") ?? "";
                string safeTarget = target?.Replace("\"", "'") ?? ""; 
                string eventId = action?.Replace(" ", "_") ?? "General_Event";

                string leefHeader = $"LEEF:1.0|GlassHouse|EnvanterAPI|1.0|{eventId}|";
                string timeStr = DateTime.Now.ToString("MMM dd yyyy HH:mm:ss", new System.Globalization.CultureInfo("en-US"));
                
                string leefAttributes = $"devTimeFormat=MMM dd yyyy HH:mm:ss\tdevTime={timeStr}\tusrName={username}\tsrc={clientIp}\ttarget={safeTarget}\tstatus={status}\tmsg={safeDetails}";

                string fullLeefLog = leefHeader + leefAttributes;
                byte[] bytes = Encoding.UTF8.GetBytes(fullLeefLog);

                await _udpClient.SendAsync(bytes, bytes.Length, _syslogIp, _syslogPort);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[UYARI - SIEM BAĞLANTISI KOPTU] Syslog gönderilemedi: {ex.Message}\n");
            }
        }

        public void Dispose()
        {
            _udpClient?.Dispose();
        }
    }
}