using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class LdapSetting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "GlassHouse AD";

        [Required]
        [MaxLength(100)]
        public string ServerIp { get; set; } = string.Empty;

        [Required]
        public int Port { get; set; } = 389;

        [Required]
        [MaxLength(50)]
        public string CommonNameIdentifier { get; set; } = "sAMAccountName";

        [Required]
        [MaxLength(200)]
        public string DistinguishedName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string BindType { get; set; } = "Regular";

        [Required]
        [MaxLength(200)]
        public string UserDn { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public bool SecureConnection { get; set; } = false;
    }
}