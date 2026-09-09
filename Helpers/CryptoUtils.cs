using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MovieManagerDesktop.Helpers
{
    public static class CryptoUtils
    {
        // Multi-stage runtime reconstruction to prevent static signature matching and string scanning
        private static readonly byte[] MaskedKey = new byte[] {
            55, 53, 44, 51, 63, 55, 59, 52, 59, 61, 63, 40, 5, 41, 63, 57,
            40, 63, 46, 5, 49, 63, 35, 5, 107, 104, 105, 110, 111, 108, 109, 98
        };
        private static readonly byte[] MaskedIv = new byte[] {
            200, 202, 211, 204, 192, 200, 196, 203, 196, 194, 192, 215, 250, 204, 211, 132
        };

        private static byte[] GetKeyBytes()
        {
            byte[] key = new byte[MaskedKey.Length];
            for (int i = 0; i < MaskedKey.Length; i++)
            {
                key[i] = (byte)(MaskedKey[i] ^ 0x5A);
            }
            return key;
        }

        private static byte[] GetIvBytes()
        {
            byte[] iv = new byte[MaskedIv.Length];
            for (int i = 0; i < MaskedIv.Length; i++)
            {
                iv[i] = (byte)(MaskedIv[i] ^ 0xA5);
            }
            return iv;
        }

        public static string? Decrypt(string encryptedBase64)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(encryptedBase64)) return null;
                byte[] cipherBytes = Convert.FromBase64String(encryptedBase64.Trim());

                using var aes = Aes.Create();
                aes.Key = GetKeyBytes();
                aes.IV = GetIvBytes();
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
                aes.Key = GetKeyBytes();
                aes.IV = GetIvBytes();
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

        /// <summary>
        /// Hardware- and user-bound DPAPI encryption preventing cross-machine or cross-user license duplication.
        /// </summary>
        public static byte[]? ProtectLocalData(byte[] userData, byte[]? optionalEntropy = null)
        {
            try
            {
                return ProtectedData.Protect(userData, optionalEntropy, DataProtectionScope.CurrentUser);
            }
            catch (Exception ex)
            {
                Services.LoggerService.Error("[CryptoUtils] DPAPI Protect failed", ex);
                return null;
            }
        }

        /// <summary>
        /// Unprotects DPAPI encrypted data bound to the current Windows user and hardware entropy.
        /// </summary>
        public static byte[]? UnprotectLocalData(byte[] encryptedData, byte[]? optionalEntropy = null)
        {
            try
            {
                return ProtectedData.Unprotect(encryptedData, optionalEntropy, DataProtectionScope.CurrentUser);
            }
            catch (Exception ex)
            {
                Services.LoggerService.Error("[CryptoUtils] DPAPI Unprotect failed", ex);
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
