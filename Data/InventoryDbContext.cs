using Microsoft.EntityFrameworkCore;
using InventoryAPI.Models;

namespace InventoryAPI.Data
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
        {
        }

        public DbSet<Device> Devices { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<LdapSetting> LdapSettings { get; set; } // OPTİMİZASYON: Gereksiz uzun isim temizlendi
        public DbSet<User> Users { get; set; } 

        // === 🛡️ VERİTABANI DBA (MİMARİ) KURALLARI ===
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- 0. CANLI SUNUCU DİL ZIRHI (TÜRKÇE COLLATION) ---
            // Veritabanı sıfırdan oluşturulurken Ş, Ğ, Ç gibi harflerde patlamaması ve
            // arama işlemlerinin (Search) kusursuz çalışması için SQL'e Türkçe emri veriyoruz!
            modelBuilder.UseCollation("Turkish_CI_AS");

            base.OnModelCreating(modelBuilder);

            // --- 1. VIP KURUCU HESABI (SEED DATA) ---
            // Sistem canlıda ilk defa ayağa kalktığında senin VIP hesabını otomatik olarak SQL'e yazar.
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "tayfun.kaydi",
                    FirstName = "Tayfun",
                    LastName = "Kaydı",
                    Department = "Cyber Security",
                    Role = "SuperAdmin",
                    IsApproved = true,
                    PasswordHash = "KURUCU_HESAP"
                }
            );

            // --- 2. KLON KAYIT ENGELLEYİCİLER (UNIQUE INDEX & MAX LENGTH) ---
            // SQL'in şişmemesi ve verilerin tutarlı kalması için kolon boylarını sınırlayıp, benzersizlik mührü basıyoruz.
            modelBuilder.Entity<User>().Property(u => u.Username).HasMaxLength(100);
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<Device>().Property(d => d.IpAddress).HasMaxLength(100);
            modelBuilder.Entity<Device>()
                .HasIndex(d => d.IpAddress)
                .IsUnique();

            modelBuilder.Entity<Device>().Property(d => d.SerialNumber).HasMaxLength(100);
            modelBuilder.Entity<Device>()
                .HasIndex(d => d.SerialNumber)
                .IsUnique();

            // --- 3. BAĞIMSIZ LOG MİMARİSİ (FOREIGN KEY İPTALİ) ---
            // Cihaz veya kullanıcı silinirse, geçmişe dönük logların silinmesini ve sistemin çökmesini engelliyoruz!
            modelBuilder.Entity<AuditLog>()
                .HasOne<User>() 
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .IsRequired(false) // Kullanıcı ID'si boş (null) veya 0 olsa bile kaydeder
                .OnDelete(DeleteBehavior.NoAction); // Kritik zırh: Silme işleminde loglara dokunma!
        }
    }
}