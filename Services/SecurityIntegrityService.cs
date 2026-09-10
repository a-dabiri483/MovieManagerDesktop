using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace MovieManagerDesktop.Services
{
    /// <summary>
    /// Advanced security, cryptographic verification, and runtime anti-tamper subsystem.
    /// Provides asymmetric RSA signature validation, IL bytecode integrity checks,
    /// anti-debugger defenses, and anti-clock rollback detection.
    /// </summary>
    internal static class SecurityIntegrityService
    {
        // Official MovieManager Asymmetric RSA-2048 Public Key (PEM format)
        // Corresponding Private Key is strictly kept on the moviemanager.ir server.
        private const string RsaPublicKeyPem = 
            "-----BEGIN PUBLIC KEY-----\n" +
            "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA1lfuhAnYRrwbVRQIjzM/\n" +
            "nBUPv/+j7NycazirN/gYTaZswXtuFsvrEs0H/Rh8FxopYntPUoSZyYyd9zbXxgRY\n" +
            "pCWwtIlsWyBhGEjVrXAIlT4jn44Lgkm9It3mF6ptKH0fmIedzKnUOmL6xb5DDzHA\n" +
            "Mi3n1UFLkX2WkyL1LCUPtkFgzp3tAziKTRHOtbu/8rAq3s2yJQJuksBcY0sVlkld\n" +
            "U21M0Bj+XBQhC+l0YB9OIiNt58cEnUVojzdojK/ze11q+gnS+RLj2bOGavvZG7SA\n" +
            "uD1Ht+fdLwyYoA1IWOCaUNj54/Q4SVFksFXxr8fgedZBS4Oo6H3OOny3Zm1dh3t6\n" +
            "XQIDAQAB\n" +
            "-----END PUBLIC KEY-----";

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] ref bool isDebuggerPresent);

        /// <summary>
        /// Validates if an active debugger or memory-injection tool is attached to the process.
        /// </summary>
        public static bool IsDebuggerAttached()
        {
            if (Debugger.IsAttached) return true;
            try
            {
                if (IsDebuggerPresent()) return true;
                bool remote = false;
                CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref remote);
                if (remote) return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Cryptographically validates the server's RSA-2048 digital signature on the license token.
        /// </summary>
        public static bool VerifyRsaToken(string token, string expectedHwid, out JsonDocument? doc, out string? errorMessage)
        {
            doc = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(token))
            {
                errorMessage = "توکن اعتبارسنجی خالی است.";
                return false;
            }

            // Expected format: MMRSA1.<base64Payload>.<base64Signature>
            if (!token.StartsWith("MMRSA1.", StringComparison.Ordinal))
            {
                errorMessage = "فرمت توکن اعتبارسنجی جدید نیست.";
                return false;
            }

            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                errorMessage = "ساختار توکن دیجیتال نامعتبر است.";
                return false;
            }

            try
            {
                byte[] payloadBytes = Convert.FromBase64String(parts[1]);
                byte[] signatureBytes = Convert.FromBase64String(parts[2]);

                using var rsa = RSA.Create();
                rsa.ImportFromPem(RsaPublicKeyPem);

                bool isValidSignature = rsa.VerifyData(
                    payloadBytes,
                    signatureBytes,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                if (!isValidSignature)
                {
                    errorMessage = "امضای دیجیتال سرور تایید نشد (توکن نامعتبر یا دستکاری شده است).";
                    return false;
                }

                // Signature is 100% genuine! Now parse the payload JSON
                string payloadJson = Encoding.UTF8.GetString(payloadBytes);
                var parsed = JsonDocument.Parse(payloadJson);
                var root = parsed.RootElement;

                string tokenHwid = root.TryGetProperty("hwid", out var hElem) ? hElem.GetString() ?? "" : "";
                if (!string.Equals(tokenHwid, expectedHwid, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"شناسه سخت‌افزاری توکن ({tokenHwid}) با شناسه این سیستم تطابق ندارد.";
                    return false;
                }

                if (root.TryGetProperty("expires_at", out var expElem) && expElem.ValueKind != JsonValueKind.Null)
                {
                    string expStr = expElem.GetString() ?? "";
                    if (!string.IsNullOrEmpty(expStr) && !expStr.Contains("مادام") && DateTime.TryParse(expStr, System.Globalization.CultureInfo.InvariantCulture, out var expDate))
                    {
                        if (DateTime.Now > expDate)
                        {
                            errorMessage = $"مدت زمان اشتراک در تاریخ {expDate} به پایان رسیده است.";
                            return false;
                        }
                    }
                }

                doc = parsed;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"خطا در رمزگشایی امضای دیجیتال: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Inspects method IL bytecode in RAM to detect dnSpy/Reflexil patches (e.g. replacing logic with 'ldc.i4.1; ret;').
        /// </summary>
        public static bool VerifyMethodBytecodeIntegrity(MethodInfo? method, int minExpectedIlLength)
        {
            if (method == null) return false;
            try
            {
                MethodBody? body = method.GetMethodBody();
                if (body == null) return false;

                byte[]? il = body.GetILAsByteArray();
                if (il == null || il.Length < minExpectedIlLength)
                {
                    // Truncated or patched method
                    return false;
                }

                // Check common crack patterns:
                // Pattern 1: ldc.i4.1 (0x17) followed by ret (0x2A) -> 2 bytes
                if (il.Length == 2 && il[0] == 0x17 && il[1] == 0x2A) return false;
                // Pattern 2: ldc.i4.s 1 (0x1F, 0x01) followed by ret (0x2A) -> 3 bytes
                if (il.Length == 3 && il[0] == 0x1F && il[1] == 0x01 && il[2] == 0x2A) return false;
                // Pattern 3: nop (0x00) followed by ret (0x2A)
                if (il.Length <= 4 && il[il.Length - 1] == 0x2A) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enforces security integrity: shuts down immediately if tampering or illegal debugging is detected.
        /// </summary>
        public static void AssertRuntimeIntegrity()
        {
            if (IsDebuggerAttached())
            {
                Environment.FailFast("Process termination due to unauthorized runtime instrumentation.");
            }

            // Verify LicenseManagerService critical methods IL lengths
            var t = typeof(LicenseManagerService);
            var m1 = t.GetMethod("IsLicenseValid", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var m2 = t.GetMethod("EnsureProFeature", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            // IsLicenseValid has at least 35 bytes of IL instructions
            if (m1 != null && !VerifyMethodBytecodeIntegrity(m1, 25))
            {
                Environment.FailFast("Security integrity violation: Core license method has been modified.");
            }

            // EnsureProFeature has at least 30 bytes of IL instructions
            if (m2 != null && !VerifyMethodBytecodeIntegrity(m2, 20))
            {
                Environment.FailFast("Security integrity violation: Feature gatekeeper method has been modified.");
            }
        }

        /// <summary>
        /// Detects if system clock was rolled back to circumvent subscription expiration.
        /// </summary>
        public static bool CheckClockRollback(DateTime? lastKnownValidTime)
        {
            if (!lastKnownValidTime.HasValue) return false;

            // If current system time is more than 2 hours BEFORE last recorded valid time, flag as rollback
            if (DateTime.Now < lastKnownValidTime.Value.AddHours(-2))
            {
                return true;
            }

            return false;
        }
    }
}
