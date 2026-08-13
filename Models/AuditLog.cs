using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryAPI.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        // 3. MAYIN İMHA EDİLDİ VE ZIRHLANDI: [Required] kaldırıldı ve 'int?' yapıldı!
        // Eğer bir personel sistemden silinirse, geçmiş logları silinmez, 
        // buradaki UserId güvenli bir şekilde 'null'a düşer ve sistem çökmekten kurtulur.
        public int? UserId { get; set; } 

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(100)] // 2. MAYIN İMHA EDİLDİ: Aksiyon adı 100 karakteri geçemez.
        public string Action { get; set; } = string.Empty; 

        [MaxLength(2000)] // 2. MAYIN İMHA EDİLDİ: Detaylar veritabanını şişirmesin diye sınırlandırıldı.
        public string? Details { get; set; } 

        // 1. MAYIN İMHA EDİLDİ: Loglar artık siber güvenlik standardı olan UTC ile tutuluyor!
        public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    }
}