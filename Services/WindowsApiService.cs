using System;
using System.Runtime.InteropServices;

namespace MovieManagerDesktop.Services
{
    public static class WindowsApiService
    {
        // ثابت‌های ویژگی‌های فایل
        public const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
        public const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
        public const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
        
        // تنظیم ویژگی‌های فایل
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetFileAttributes(string lpFileName, uint dwFileAttributes);
        
        private const uint SHCNE_UPDATEDIR = 0x00001000;
        private const uint SHCNF_PATH = 0x0005;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, string dwItem1, IntPtr dwItem2);

        public static void RefreshFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATH, path, IntPtr.Zero);
        }

        public static void ApplyIconToFolder(string folderPath, string iconFileName = "folder_icon.ico")
        {
            string desktopIniPath = System.IO.Path.Combine(folderPath, "desktop.ini");
            string desktopIniContent = $@"[.ShellClassInfo]
IconResource={iconFileName},0

[ViewState]
Mode=
Vid=
FolderType=Generic
";
            System.IO.File.WriteAllText(desktopIniPath, desktopIniContent);

            SetFileAttributes(desktopIniPath, FILE_ATTRIBUTE_SYSTEM | FILE_ATTRIBUTE_HIDDEN);
            SetFileAttributes(folderPath, FILE_ATTRIBUTE_READONLY);

            RefreshFolder(folderPath);
        }
    }
}
