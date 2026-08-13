using System.ComponentModel.DataAnnotations; 

namespace InventoryAPI.DTOs
{
    // --- 1. YENİ YEREL KAYIT (LOCAL REGISTER) ÇANTASI 🛡️ ---
    // (Login ekranından dışarıdan kayıt olan kullanıcılar için kullanılır)
    public class LocalRegisterDto
    {
        [Required(ErrorMessage = "Ad alanı zorunludur.")]
        [MaxLength(50, ErrorMessage = "Ad en fazla 50 karakter olabilir.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad alanı zorunludur.")]
        [MaxLength(50, ErrorMessage = "Soyad en fazla 50 karakter olabilir.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Departman alanı zorunludur.")]
        [MaxLength(50, ErrorMessage = "Departman en fazla 50 karakter olabilir.")]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        [MaxLength(100, ErrorMessage = "Kullanıcı adı en fazla 100 karakter olabilir.")]
        public string Username { get; set; } = string.Empty;

        // YEREL KAYIT İÇİN ŞİFRE EKLENDİ!
        [Required(ErrorMessage = "Şifre alanı zorunludur.")]
        [MaxLength(100, ErrorMessage = "Şifre en fazla 100 karakter olabilir.")]
        public string Password { get; set; } = string.Empty;
        
        // Not: 'Role' alanı güvenlik gereği buraya eklenmedi.
        // Backend bu kişilere otomatik olarak onaysız ve en düşük yetkiyi atayacak.
    }

    // --- 2. AD PERSONELİ EKLEME (ADMIN) ÇANTASI ---
    // (İçerideki Adminlerin sisteme AD personeli dahil etmesi için)
    public class UserRegisterDto
    {
        [Required(ErrorMessage = "Ad alanı zorunludur.")]
        [MaxLength(50, ErrorMessage = "Ad en fazla 50 karakter olabilir.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad alanı zorunludur.")]
        [MaxLength(50, ErrorMessage = "Soyad en fazla 50 karakter olabilir.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kullanıcı adı (AD Username) zorunludur.")]
        [MaxLength(100, ErrorMessage = "Kullanıcı adı en fazla 100 karakter olabilir.")]
        public string Username { get; set; } = string.Empty;

        // OPTİMİZASYON: AD şifreleri bilinmediği için şifre alanı yok.

        [Required(ErrorMessage = "Departman alanı zorunludur.")]
        [MaxLength(50)]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kullanıcı rolü zorunludur.")]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty;
    }
    
    // --- 3. GİRİŞ YAPMA (LOGIN) ÇANTASI ---
    public class UserLoginDto
    {
        [Required(ErrorMessage = "Lütfen kullanıcı adınızı girin.")]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lütfen şifrenizi girin.")]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;
    }
}