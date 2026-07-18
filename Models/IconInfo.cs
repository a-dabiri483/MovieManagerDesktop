using System.IO;

namespace MovieManagerDesktop.Models
{
    public class IconInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string FullPath => System.IO.Path.GetFullPath(Path);
        public bool IsIconFile => Path.ToLower().EndsWith(".ico");
        public string FileExtension => System.IO.Path.GetExtension(Path).ToLower();

        /// <summary>اندازه فایل (کش شده در زمان ساخت آبجکت)</summary>
        public long FileSize { get; }

        public IconInfo(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileNameWithoutExtension(path);
            FileSize = File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        public IconInfo() { }
    }
}
