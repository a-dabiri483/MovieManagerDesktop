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

        /// <summary>
        /// Encrypts a plain string with current Windows user DPAPI and prepends 'dpapi:' prefix.
        /// If input is empty or encryption fails, returns the original string.
        /// </summary>
        public static string ProtectString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[]? protectedBytes = ProtectLocalData(plainBytes);
                if (protectedBytes == null) return plainText;
                return "dpapi:" + Convert.ToBase64String(protectedBytes);
            }
            catch (Exception ex)
            {
                Services.LoggerService.Error("[CryptoUtils] ProtectString failed", ex);
                return plainText;
            }
        }

        /// <summary>
        /// Decrypts a DPAPI encrypted string prefixed with 'dpapi:'.
        /// If not prefixed, returns the string as is (for seamless backward compatibility).
        /// </summary>
        public static string UnprotectString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            if (!cipherText.StartsWith("dpapi:", StringComparison.Ordinal)) return cipherText;
            try
            {
                string b64 = cipherText.Substring(6);
                byte[] protectedBytes = Convert.FromBase64String(b64);
                byte[]? plainBytes = UnprotectLocalData(protectedBytes);
                if (plainBytes == null) return cipherText;
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Services.LoggerService.Error("[CryptoUtils] UnprotectString failed", ex);
                return cipherText;
            }
        }

        public static readonly byte[] GcmHeaderMagic = new byte[] { 0x4D, 0x4D, 0x47, 0x43, 0x4D, 0x31 }; // "MMGCM1"

        /// <summary>
        /// Encrypts arbitrary bytes using AES-256-GCM with a fresh 12-byte random nonce and 16-byte authentication tag.
        /// Output: [6B MMGCM1 Magic] + [12B Random Nonce] + [16B Auth Tag] + [N-Bytes Ciphertext].
        /// </summary>
        public static byte[] EncryptBytesGcm(byte[] plaintext)
        {
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));

            byte[] key = GetKeyBytes();
            byte[] nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);

            byte[] tag = new byte[16];
            byte[] ciphertext = new byte[plaintext.Length];

            using (var aesGcm = new AesGcm(key, 16))
            {
                aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            byte[] result = new byte[GcmHeaderMagic.Length + nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(GcmHeaderMagic, 0, result, 0, GcmHeaderMagic.Length);
            Buffer.BlockCopy(nonce, 0, result, GcmHeaderMagic.Length, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, GcmHeaderMagic.Length + nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, result, GcmHeaderMagic.Length + nonce.Length + tag.Length, ciphertext.Length);
            return result;
        }

        /// <summary>
        /// Decrypts AES-256-GCM payload with integrity verification via authentication tag.
        /// Returns null if tampering is detected or format is invalid.
        /// </summary>
        public static byte[]? DecryptBytesGcm(byte[] payload)
        {
            try
            {
                if (payload == null || payload.Length < GcmHeaderMagic.Length + 12 + 16)
                    return null;

                // Check Magic Header
                for (int i = 0; i < GcmHeaderMagic.Length; i++)
                {
                    if (payload[i] != GcmHeaderMagic[i])
                        return null;
                }

                byte[] nonce = new byte[12];
                Buffer.BlockCopy(payload, GcmHeaderMagic.Length, nonce, 0, 12);

                byte[] tag = new byte[16];
                Buffer.BlockCopy(payload, GcmHeaderMagic.Length + 12, tag, 0, 16);

                int cipherOffset = GcmHeaderMagic.Length + 12 + 16;
                int cipherLength = payload.Length - cipherOffset;
                byte[] ciphertext = new byte[cipherLength];
                Buffer.BlockCopy(payload, cipherOffset, ciphertext, 0, cipherLength);

                byte[] key = GetKeyBytes();
                byte[] plaintext = new byte[cipherLength];

                using (var aesGcm = new AesGcm(key, 16))
                {
                    aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
                }

                return plaintext;
            }
            catch (CryptographicException cex)
            {
                Services.LoggerService.Error("[CryptoUtils] AES-GCM authentication failed (tampered or invalid payload)", cex);
                return null;
            }
            catch (Exception ex)
            {
                Services.LoggerService.Error("[CryptoUtils] AES-GCM decryption failed", ex);
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
