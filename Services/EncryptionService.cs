using System.Security.Cryptography; 
using System.Text;                  
using System.IO;                    
using System.Linq;
using System;
using Microsoft.Extensions.Configuration;

namespace InventoryAPI.Services
{
    public class EncryptionService
    {
        private readonly string _key;

        public EncryptionService(IConfiguration configuration)
        {
            _key = configuration.GetSection("Encryption:Key").Value 
                   ?? throw new Exception("Encryption key bulunamadı!");
        }

        // --- GÜVENLİ ANAHTAR ÜRETİCİ (SHA-256) ---
        // Şifrenin içinde Türkçe karakter olsa bile çökmeyi engeller!
        private byte[] GetValidAesKey()
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(_key));
            }
        }

        // --- ŞİFRELEME (ENCRYPT) ---
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            byte[] iv;
            byte[] array;

            using (Aes aes = Aes.Create())
            {
                // 1. MAYIN İMHA EDİLDİ: Kusursuz 32 Byte anahtar bağlandı
                aes.Key = GetValidAesKey();
                aes.GenerateIV(); 
                iv = aes.IV;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }
                        array = memoryStream.ToArray();
                    }
                }
            }
            
            return Convert.ToBase64String(iv.Concat(array).ToArray());
        }

        // --- ŞİFRE ÇÖZME (DECRYPT) ---
        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try 
            {
                byte[] fullCipher = Convert.FromBase64String(cipherText);
                byte[] iv = new byte[16];
                byte[] cipher = new byte[fullCipher.Length - 16];

                Array.Copy(fullCipher, iv, 16);
                Array.Copy(fullCipher, 16, cipher, 0, cipher.Length);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = GetValidAesKey(); // Çözerken de aynı kusursuz anahtar kullanılıyor
                    aes.IV = iv;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream(cipher))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader(cryptoStream))
                            {
                                return streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch
            {
                return "Şifre Çözülemedi"; 
            }
        }
    }
}