using System;
using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class Device
    {
        [Key]
        public int Id { get; set; }

        public string Customer { get; set; } = string.Empty;
        
        // ŞAH MAT 1: int olan Site'yi string? yaptık
        public string? Site { get; set; } 

        public string Vendor { get; set; } = string.Empty;
        
        // ŞAH MAT 2: Cihaz tablosuna Version sütununu ekledik
        public string? Version { get; set; } 

        public string Name { get; set; } = string.Empty;
        public string? Hostname { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? Gateway { get; set; }
        public string Username { get; set; } = string.Empty;
        
        public string EncryptedPassword { get; set; } = string.Empty;
        
        public string VisibleTo { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}