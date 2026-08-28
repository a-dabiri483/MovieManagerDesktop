using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MovieManagerDesktop.Helpers
{
    public static class CryptoUtils
    {
        // 32-byte Key and 16-byte IV constructed dynamically to prevent static string inspection
        private static readonly byte[] KeyBytes = new byte[] {
            109, 111, 118, 105, 101, 109, 97, 110, 97, 103, 101, 114, 95, 115, 101, 99,
            114, 101, 116, 95, 107, 101, 121, 95, 49, 50, 51, 52, 53, 54, 55, 56
        }; // "moviemanager_secret_key_12345678"
        
        private static readonly byte[] IvBytes = new byte[] {
            109, 111, 118, 105, 101, 109, 97, 110, 97, 103, 101, 114, 95, 105, 118, 33
        }; // "moviemanager_iv!"

        public static string? Decrypt(string encryptedBase64)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(encryptedBase64)) return null;
                byte[] cipherBytes = Convert.FromBase64String(encryptedBase64.Trim());

                using var aes = Aes.Create();
                aes.Key = KeyBytes;
                aes.IV = IvBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Services.LoggerService.Error("[CryptoUtils] Decryption failed", ex);
                return null;
            }
        }

        public static string? Encrypt(string plainText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(plainText)) return null;
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

                using var aes = Aes.Create();
                aes.Key = KeyBytes;
                aes.IV = IvBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var encryptor = aes.CreateEncryptor();
                byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                return Convert.ToBase64String(cipherBytes);
            }
            catch (Exception ex)
            {
                Services.LoggerService.Error("[CryptoUtils] Encryption failed", ex);
                return null;
            }
        }

        public static string GetObfuscatedSourceUrl()
        {
            // Primary URL: https://moviemanager.ir/web/admin_api.php?action=public_proxies
            char[] chars = "seixorp_cilbup=noitca?php.ipa_nimda/bew/ri.reganameivom//:sptth".ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        public static string GetBackupObfuscatedSourceUrl()
        {
            // Backup URL: https://raw.githubusercontent.com/a-dabiri483/AppConfig/main/proxies.txt
            char[] chars = "txt.seixorp/niam/gifnoCppA/384iribad-a/moc.tnetnocresubuhtig.war//:sptth".ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }
    }
}
