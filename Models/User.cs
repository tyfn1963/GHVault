using System;
using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)] // DTO ile tam uyumlu!
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Department { get; set; } = "Cyber Security"; // Arayüzdeki departman ismiyle eşleşti

        [Required]
        [MaxLength(100)] // Veritabanındaki Unique kuralımız için sınırlandı
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)] // Şifreler (veya AD yer tutucuları) için ideal güvenlik sınırı
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Görevli"; // Sistemdeki en düşük ve güvenli varsayılan rol

        // Zaman kayması hatası giderildi, Siber Güvenlik standartlarına çekildi
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // 🛡️ Kilit Nokta: Yeni kayıtlar varsayılan olarak onaysızdır.
        public bool IsApproved { get; set; } = false;
    }
}