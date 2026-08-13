using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.DTOs
{
    // --- 1. ÇIKIŞ ÇANTASI (Frontend'e gönderilen veri - Şifresiz ve Güvenli) ---
    public class DeviceDto
    {
        public int Id { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string? Site { get; set; } // EKLENDİ
        public string Vendor { get; set; } = string.Empty;
        public string? Version { get; set; } // EKLENDİ
        public string Name { get; set; } = string.Empty;
        public string? Hostname { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? Gateway { get; set; }
        public string Username { get; set; } = string.Empty;
        public string VisibleTo { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? SerialNumber { get; set; }
        public string? Description { get; set; }
    }

    // --- 2. GİRİŞ ÇANTASI (Yeni cihaz eklerken POST ile gelen veri) ---
    public class DeviceCreateDto
    {
        [Required(ErrorMessage = "Müşteri adı zorunludur.")]
        [MaxLength(100, ErrorMessage = "Müşteri adı en fazla 100 karakter olabilir.")]
        public string Customer { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Site { get; set; } // EKLENDİ

        [Required(ErrorMessage = "Marka (Vendor) zorunludur.")]
        [MaxLength(100)]
        public string Vendor { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Version { get; set; } // EKLENDİ

        [Required(ErrorMessage = "Cihaz adı zorunludur.")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Hostname { get; set; }

        // YENİ HALİ (Sınır kalktı):
        [Required(ErrorMessage = "IP Adresi zorunludur.")]
        public string IpAddress { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Gateway { get; set; }

        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Cihaz şifresi zorunludur.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Departman (VisibleTo) zorunludur.")]
        [MaxLength(50)]
        public string VisibleTo { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "Seri Numarası en fazla 100 karakter olabilir.")]
        public string? SerialNumber { get; set; }

        [MaxLength(500, ErrorMessage = "Açıklama 500 karakterden uzun olamaz.")]
        public string? Description { get; set; }
    }

    // --- 3. GÜNCELLEME ÇANTASI (PUT ile gelen veri) ---
    public class DeviceUpdateDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Customer { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Site { get; set; } // GÜNCELLENDİ (String? yapıldı)

        [Required]
        [MaxLength(100)]
        public string Vendor { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Version { get; set; } // EKLENDİ

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Hostname { get; set; }

        [Required]
        public string IpAddress { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Gateway { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        public string? Password { get; set; }

        [Required]
        [MaxLength(50)]
        public string VisibleTo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? SerialNumber { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }

    // --- 4. DEVRETME ÇANTASI ---
    public class TransferDto
    {
        [Required(ErrorMessage = "Hedef departman zorunludur.")]
        [MaxLength(50)]
        public string VisibleTo { get; set; } = string.Empty;
    }
}