using System;

namespace MovieManagerDesktop.Models
{
    public class LicenseInfo
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string PlanTitle { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public bool IsLifetime { get; set; }
        public bool IsActivated { get; set; }
        public string BoundHwid { get; set; } = string.Empty;
        public string OfflineToken { get; set; } = string.Empty;
        public DateTime LastVerifiedAt { get; set; } = DateTime.MinValue;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        public bool IsValid
        {
            get
            {
                if (!IsActivated || string.IsNullOrWhiteSpace(LicenseKey))
                    return false;

                if (IsLifetime)
                    return true;

                if (ExpiresAt.HasValue && DateTime.Now > ExpiresAt.Value)
                    return false;

                return true;
            }
        }

        public int? DaysRemaining
        {
            get
            {
                if (IsLifetime) return null;
                if (!ExpiresAt.HasValue) return null;
                var diff = ExpiresAt.Value - DateTime.Now;
                return Math.Max(0, (int)Math.Ceiling(diff.TotalDays));
            }
        }

        public string StatusSummary
        {
            get
            {
                if (!IsActivated)
                    return "نرم‌افزار فعال نشده است (نسخه محدود)";
                if (IsLifetime)
                    return "لایسنس دائمی و مادام‌العمر فعال است ✓";
                if (ExpiresAt.HasValue)
                {
                    if (DateTime.Now > ExpiresAt.Value)
                        return "اعتبار زمانی لایسنس به پایان رسیده است ✕";
                    int days = DaysRemaining ?? 0;
                    return $"فعال ({days} روز باقیمانده تا انقضا) ✓";
                }
                return "لایسنس فعال است ✓";
            }
        }

        public string MaskedLicenseKey
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LicenseKey))
                    return "ثبت نشده";

                var parts = LicenseKey.Split('-');
                if (parts.Length == 5)
                {
                    // MM-XXXX-****-****-XXXX
                    return $"{parts[0]}-{parts[1]}-****-****-{parts[4]}";
                }
                return LicenseKey;
            }
        }
    }
}
