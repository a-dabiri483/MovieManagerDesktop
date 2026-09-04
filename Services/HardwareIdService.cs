using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace MovieManagerDesktop.Services
{
    /// <summary>
    /// Generates a robust, unique, hardware-bound identifier (HWID) for the current Windows machine.
    /// Combines motherboard, processor, OS installation GUID, and system volume serial number.
    /// </summary>
    public static class HardwareIdService
    {
        private static string? _cachedHwid;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetVolumeInformation(
            string lpRootPathName,
            StringBuilder? lpVolumeNameBuffer,
            int nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            StringBuilder? lpFileSystemNameBuffer,
            int nFileSystemNameSize);

        /// <summary>
        /// Retrieves or calculates the hardware ID in the format: MM-HWID-XXXX-XXXX-XXXX-XXXX
        /// </summary>
        public static string GetHardwareId()
        {
            if (!string.IsNullOrEmpty(_cachedHwid))
            {
                return _cachedHwid;
            }

            var sb = new StringBuilder();

            // 1. Windows MachineGuid from Registry (stable across reboots, unique per Windows installation)
            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                           .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid")?.ToString();
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    sb.Append("GUID:").Append(guid.Trim()).Append(';');
                }
            }
            catch { }

            // 2. System Drive Volume Serial Number (C:\)
            try
            {
                string sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
                if (GetVolumeInformation(sysDrive, null, 0, out uint serial, out _, out _, null, 0))
                {
                    sb.Append("VOL:").Append(serial.ToString("X8")).Append(';');
                }
            }
            catch { }

            // 3. Processor Identifier and Core Count
            try
            {
                string? procId = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
                string? procArch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
                int cores = Environment.ProcessorCount;
                sb.Append("CPU:").Append(procId).Append(':').Append(procArch).Append(':').Append(cores).Append(';');
            }
            catch { }

            // 4. Motherboard / BIOS Info from Registry
            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                           .OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                var baseboard = key?.GetValue("BaseBoardProduct")?.ToString();
                var systemProd = key?.GetValue("SystemProductName")?.ToString();
                if (!string.IsNullOrWhiteSpace(baseboard) || !string.IsNullOrWhiteSpace(systemProd))
                {
                    sb.Append("MB:").Append(baseboard).Append('/').Append(systemProd).Append(';');
                }
            }
            catch { }

            // Fallback safety if somehow all components returned empty
            if (sb.Length == 0)
            {
                sb.Append("FALLBACK:").Append(Environment.MachineName).Append(';').Append(Environment.UserName);
            }

            // Cryptographic SHA-256 Hash
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            string hex = Convert.ToHexString(hashBytes); // 64-char uppercase hex

            // Output structured HWID: MM-HWID-XXXX-XXXX-XXXX-XXXX (16 chars from hex)
            _cachedHwid = $"MM-HWID-{hex.Substring(0, 4)}-{hex.Substring(4, 4)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}";
            return _cachedHwid;
        }
    }
}
