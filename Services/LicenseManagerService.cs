using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using MovieManagerDesktop.Helpers;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Views;

namespace MovieManagerDesktop.Services
{
    /// <summary>
    /// Core security and licensing manager for MovieManager Windows desktop edition.
    /// Handles HWID binding, online activation, offline cryptographic token verification, and secure local storage.
    /// </summary>
    public static class LicenseManagerService
    {
        public const int FreeTierMediaLimit = 15;
        private const string PrimaryApiUrl = "https://moviemanager.ir/license/api.php";
        private static readonly object _lock = new();
        private static LicenseInfo? _cachedLicense;

        public static event EventHandler<LicenseInfo>? LicenseStatusChanged;

        /// <summary>
        /// Helper to verify whether a Pro feature is allowed. If not, displays a friendly toast and opens the license activation window.
        /// </summary>
        public static bool EnsureProFeature(string featureName)
        {
            if (IsLicenseValid())
            {
                return true;
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                ToastService.Instance.ShowWarning($"«{featureName}» نیازمند نسخه Pro است.");
                var win = new LicenseActivationWindow();
                WindowHelper.SafeShowDialog(win);
            });

            return false;
        }

        private static string GetLicenseFilePath()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string folder = Path.Combine(localAppData, "MovieManager");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                return Path.Combine(folder, "license.dat");
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.dat");
            }
        }

        /// <summary>
        /// Retrieves the current license information. Returns an inactive LicenseInfo if not activated or tampered.
        /// </summary>
        public static LicenseInfo GetCurrentLicense()
        {
            if (_cachedLicense != null)
            {
                return _cachedLicense;
            }

            lock (_lock)
            {
                if (_cachedLicense != null)
                {
                    return _cachedLicense;
                }

                _cachedLicense = LoadAndVerifyLicenseFromDisk();
                return _cachedLicense;
            }
        }

        /// <summary>
        /// Checks if the software has a valid, active, non-expired license bound to this machine.
        /// </summary>
        public static bool IsLicenseValid()
        {
            var lic = GetCurrentLicense();
            return lic.IsValid;
        }

        /// <summary>
        /// Activates or verifies license directly by computer HWID without requiring the user to type a key.
        /// </summary>
        public static async Task<(bool Success, string Message, LicenseInfo? Info)> ActivateByHwidAsync()
        {
            string hwid = HardwareIdService.GetHardwareId();
            string deviceName = $"{Environment.MachineName} ({Environment.OSVersion.VersionString})";

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true
                };

                string jsonPayload = $"{{\"hwid\":\"{EscapeJson(hwid)}\",\"device_name\":\"{EscapeJson(deviceName)}\"}}";
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{PrimaryApiUrl}?action=activate_by_hwid", content);
                string jsonResp = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(jsonResp))
                {
                    return (false, "پاسخی از سرور دریافت نشد. لطفاً اتصال اینترنت خود را بررسی کنید.", null);
                }

                using var doc = JsonDocument.Parse(jsonResp);
                var root = doc.RootElement;

                bool success = root.TryGetProperty("success", out var sElem) && sElem.GetBoolean();
                if (!success)
                {
                    string err = root.TryGetProperty("error", out var errElem) ? errElem.GetString() ?? "لایسنس فعالی برای این سیستم یافت نشد." : "لایسنس فعالی برای این سیستم یافت نشد.";
                    return (false, err, null);
                }

                string key = root.TryGetProperty("license_key", out var kElem) ? kElem.GetString() ?? "" : "";
                string planTitle = root.TryGetProperty("plan_title", out var pElem) ? pElem.GetString() ?? "اشتراک فعال" : "اشتراک فعال";
                bool isLifetime = root.TryGetProperty("is_lifetime", out var lifeElem) && lifeElem.GetBoolean();
                string token = root.TryGetProperty("token", out var tokElem) ? tokElem.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(key))
                {
                    return (false, "هیچ لایسنس فعالی برای شناسه سخت‌افزاری این سیستم یافت نشد. لطفاً از طریق دکمه «خرید لایسنس» اقدام نمایید.", null);
                }

                if (!VerifyOfflineToken(token, hwid, key))
                {
                    return (false, "توکن اعتبارسنجی لایسنس تایید نشد.", null);
                }

                DateTime? expiresAt = null;
                if (!isLifetime && root.TryGetProperty("expires_at", out var expElem))
                {
                    string expStr = expElem.GetString() ?? "";
                    if (DateTime.TryParse(expStr, out var parsedDate))
                    {
                        expiresAt = parsedDate;
                    }
                }

                var newLicense = new LicenseInfo
                {
                    LicenseKey = key,
                    PlanTitle = planTitle,
                    IsLifetime = isLifetime,
                    ExpiresAt = expiresAt,
                    IsActivated = true,
                    BoundHwid = hwid,
                    OfflineToken = token,
                    LastVerifiedAt = DateTime.Now
                };

                SaveLicenseToDisk(newLicense);

                lock (_lock)
                {
                    _cachedLicense = newLicense;
                }

                LicenseStatusChanged?.Invoke(null, newLicense);

                return (true, "لایسنس با موفقیت برای این سیستم فعال گردید! ✓", newLicense);
            }
            catch (Exception ex)
            {
                LoggerService.Error("[LicenseManager] HWID activation failed", ex);
                return (false, $"خطا در برقراری ارتباط با سرور: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Activates a license key with the server and binds it to this computer's HWID.
        /// </summary>
        public static async Task<(bool Success, string Message, LicenseInfo? Info)> ActivateLicenseAsync(string licenseKey)
        {
            licenseKey = (licenseKey ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return (false, "لطفاً کلید لایسنس را وارد کنید.", null);
            }

            string hwid = HardwareIdService.GetHardwareId();
            string deviceName = $"{Environment.MachineName} ({Environment.OSVersion.VersionString})";

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true
                };

                string jsonPayload = $"{{\"license_key\":\"{EscapeJson(licenseKey)}\",\"hwid\":\"{EscapeJson(hwid)}\",\"device_name\":\"{EscapeJson(deviceName)}\"}}";
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{PrimaryApiUrl}?action=activate_license", content);
                string jsonResp = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(jsonResp))
                {
                    return (false, "پاسخی از سرور لایسنس دریافت نشد.", null);
                }

                using var doc = JsonDocument.Parse(jsonResp);
                var root = doc.RootElement;

                bool success = root.TryGetProperty("success", out var sElem) && sElem.GetBoolean();
                if (!success)
                {
                    string err = root.TryGetProperty("error", out var errElem) ? errElem.GetString() ?? "فعال‌سازی با خطا مواجه شد." : "فعال‌سازی ناموفق بود.";
                    return (false, err, null);
                }

                string key = root.TryGetProperty("license_key", out var kElem) ? kElem.GetString() ?? licenseKey : licenseKey;
                string planTitle = root.TryGetProperty("plan_title", out var pElem) ? pElem.GetString() ?? "اشتراک فعال" : "اشتراک فعال";
                bool isLifetime = root.TryGetProperty("is_lifetime", out var lifeElem) && lifeElem.GetBoolean();
                string token = root.TryGetProperty("token", out var tokElem) ? tokElem.GetString() ?? "" : "";

                DateTime? expiresAt = null;
                if (!isLifetime && root.TryGetProperty("expires_at", out var expElem))
                {
                    string expStr = expElem.GetString() ?? "";
                    if (DateTime.TryParse(expStr, out var parsedDate))
                    {
                        expiresAt = parsedDate;
                    }
                }

                var newLicense = new LicenseInfo
                {
                    LicenseKey = key,
                    PlanTitle = planTitle,
                    IsLifetime = isLifetime,
                    ExpiresAt = expiresAt,
                    IsActivated = true,
                    BoundHwid = hwid,
                    OfflineToken = token,
                    LastVerifiedAt = DateTime.Now
                };

                SaveLicenseToDisk(newLicense);

                lock (_lock)
                {
                    _cachedLicense = newLicense;
                }

                LicenseStatusChanged?.Invoke(null, newLicense);

                return (true, "لایسنس با موفقیت فعال شد و به این سیستم متصل گردید.", newLicense);
            }
            catch (Exception ex)
            {
                LoggerService.Error("[LicenseManager] Online activation failed", ex);
                return (false, $"خطا در برقراری ارتباط با سرور: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Silently validates license online in the background; falls back to offline verification if no connection.
        /// </summary>
        public static async Task<bool> VerifyLicenseAsync()
        {
            var current = GetCurrentLicense();
            if (!current.IsActivated || string.IsNullOrWhiteSpace(current.LicenseKey))
            {
                return false;
            }

            string currentHwid = HardwareIdService.GetHardwareId();

            // First verify offline token
            bool offlineValid = VerifyOfflineToken(current.OfflineToken, currentHwid, current.LicenseKey);
            if (!offlineValid)
            {
                LoggerService.Warning("[LicenseManager] Offline token verification failed for current machine.");
                return false;
            }

            // If offline is valid, attempt silent background online check
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true
                };

                string url = $"{PrimaryApiUrl}?action=verify_license&license_key={Uri.EscapeDataString(current.LicenseKey)}&hwid={Uri.EscapeDataString(currentHwid)}";
                var response = await client.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    bool isValid = root.TryGetProperty("is_valid", out var vElem) && vElem.GetBoolean();
                    if (!isValid)
                    {
                        // License was revoked, refunded, deleted, or deactivated remotely
                        LoggerService.Warning("[LicenseManager] Remote server marked license as invalid. Deactivating local license.");
                        DeactivateCurrentLicense();
                        return false;
                    }

                    // Update last verified timestamp
                    current.LastVerifiedAt = DateTime.Now;
                    SaveLicenseToDisk(current);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Network failure or timeout: allow offline continuation if offline token is valid
                LoggerService.Info($"[LicenseManager] Background online check skipped ({ex.Message}). Continuing in verified offline mode.");
            }

            return current.IsValid;
        }

        /// <summary>
        /// Clears the local license file and resets the application to the free tier.
        /// </summary>
        public static void DeactivateCurrentLicense()
        {
            try
            {
                string filePath = GetLicenseFilePath();
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("[LicenseManager] Failed to delete license file", ex);
            }

            lock (_lock)
            {
                _cachedLicense = new LicenseInfo { IsActivated = false };
            }

            LicenseStatusChanged?.Invoke(null, _cachedLicense);
        }

        /// <summary>
        /// Validates the cryptographically signed server token offline without requiring internet.
        /// </summary>
        private static bool VerifyOfflineToken(string token, string currentHwid, string expectedKey)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            try
            {
                string? decryptedJson = CryptoUtils.Decrypt(token);
                if (string.IsNullOrWhiteSpace(decryptedJson))
                {
                    return false;
                }

                using var doc = JsonDocument.Parse(decryptedJson);
                var root = doc.RootElement;

                string tokenKey = root.TryGetProperty("key", out var kElem) ? kElem.GetString() ?? "" : "";
                string tokenHwid = root.TryGetProperty("hwid", out var hElem) ? hElem.GetString() ?? "" : "";

                // Ensure token is bound to this exact HWID and key
                if (!string.Equals(tokenKey, expectedKey, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!string.Equals(tokenHwid, currentHwid, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Check expiration in token
                if (root.TryGetProperty("expires_at", out var expElem) && expElem.ValueKind != JsonValueKind.Null)
                {
                    string expStr = expElem.GetString() ?? "";
                    if (!string.IsNullOrEmpty(expStr) && !expStr.Contains("مادام") && DateTime.TryParse(expStr, out var expDate))
                    {
                        if (DateTime.Now > expDate)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeJson(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "");
        }

        private static void SaveLicenseToDisk(LicenseInfo lic)
        {
            try
            {
                string filePath = GetLicenseFilePath();
                string expiresVal = lic.ExpiresAt.HasValue ? $"\"{lic.ExpiresAt.Value:o}\"" : "null";
                string json = $"{{\"LicenseKey\":\"{EscapeJson(lic.LicenseKey)}\",\"PlanTitle\":\"{EscapeJson(lic.PlanTitle)}\",\"ExpiresAt\":{expiresVal},\"IsLifetime\":{(lic.IsLifetime ? "true" : "false")},\"IsActivated\":{(lic.IsActivated ? "true" : "false")},\"BoundHwid\":\"{EscapeJson(lic.BoundHwid)}\",\"OfflineToken\":\"{EscapeJson(lic.OfflineToken)}\",\"LastVerifiedAt\":\"{lic.LastVerifiedAt:o}\",\"CustomerEmail\":\"{EscapeJson(lic.CustomerEmail)}\",\"CustomerPhone\":\"{EscapeJson(lic.CustomerPhone)}\"}}";
                string? encrypted = CryptoUtils.Encrypt(json);

                if (!string.IsNullOrEmpty(encrypted))
                {
                    File.WriteAllText(filePath, encrypted, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error("[LicenseManager] Failed to save license file", ex);
            }
        }

        private static LicenseInfo LoadAndVerifyLicenseFromDisk()
        {
            try
            {
                string filePath = GetLicenseFilePath();
                if (!File.Exists(filePath))
                {
                    return new LicenseInfo { IsActivated = false };
                }

                string encrypted = File.ReadAllText(filePath, Encoding.UTF8);
                string? json = CryptoUtils.Decrypt(encrypted);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new LicenseInfo { IsActivated = false };
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var lic = new LicenseInfo
                {
                    LicenseKey = root.TryGetProperty("LicenseKey", out var p1) ? p1.GetString() ?? "" : "",
                    PlanTitle = root.TryGetProperty("PlanTitle", out var p2) ? p2.GetString() ?? "" : "",
                    IsLifetime = root.TryGetProperty("IsLifetime", out var p3) && p3.GetBoolean(),
                    IsActivated = root.TryGetProperty("IsActivated", out var p4) && p4.GetBoolean(),
                    BoundHwid = root.TryGetProperty("BoundHwid", out var p5) ? p5.GetString() ?? "" : "",
                    OfflineToken = root.TryGetProperty("OfflineToken", out var p6) ? p6.GetString() ?? "" : "",
                    CustomerEmail = root.TryGetProperty("CustomerEmail", out var p7) ? p7.GetString() ?? "" : "",
                    CustomerPhone = root.TryGetProperty("CustomerPhone", out var p8) ? p8.GetString() ?? "" : ""
                };

                if (root.TryGetProperty("ExpiresAt", out var pExp) && pExp.ValueKind != JsonValueKind.Null && pExp.TryGetDateTime(out var dtExp))
                {
                    lic.ExpiresAt = dtExp;
                }

                if (root.TryGetProperty("LastVerifiedAt", out var pVer) && pVer.ValueKind != JsonValueKind.Null && pVer.TryGetDateTime(out var dtVer))
                {
                    lic.LastVerifiedAt = dtVer;
                }

                if (!lic.IsActivated)
                {
                    return new LicenseInfo { IsActivated = false };
                }

                // Verify HWID matches current machine
                string currentHwid = HardwareIdService.GetHardwareId();
                if (!string.Equals(lic.BoundHwid, currentHwid, StringComparison.OrdinalIgnoreCase))
                {
                    LoggerService.Warning("[LicenseManager] HWID mismatch: License was copied from another machine.");
                    return new LicenseInfo { IsActivated = false };
                }

                // Verify offline cryptographic token
                if (!VerifyOfflineToken(lic.OfflineToken, currentHwid, lic.LicenseKey))
                {
                    LoggerService.Warning("[LicenseManager] Stored offline token is invalid or tampered.");
                    return new LicenseInfo { IsActivated = false };
                }

                return lic;
            }
            catch (Exception ex)
            {
                LoggerService.Error("[LicenseManager] Error reading license file", ex);
                return new LicenseInfo { IsActivated = false };
            }
        }
    }
}
