using System;
using System.IO;
using System.Windows;
using FlyleafLib;
using FlyleafLib.MediaPlayer;

namespace MovieManagerDesktop.Views
{
    public partial class VideoPlayerWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public Player Player { get; set; }

        public VideoPlayerWindow(string filePath)
        {
            InitializeComponent();

            string ffmpegFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FFmpeg", "x64");
            if (!Directory.Exists(ffmpegFolder))
            {
                ffmpegFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FFmpeg");
            }
            if (!Directory.Exists(ffmpegFolder))
            {
                ffmpegFolder = AppDomain.CurrentDomain.BaseDirectory;
            }

            try
            {
                SetDllDirectory(ffmpegFolder);
                Environment.SetEnvironmentVariable("PATH", ffmpegFolder + ";" + Environment.GetEnvironmentVariable("PATH"));

                Engine.Start(new EngineConfig()
                {
                    FFmpegPath = ffmpegFolder
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Engine.Start failed: {ex.Message}\n\n{ex.StackTrace}", "Flyleaf Engine Error");
                return;
            }

            Player = new Player();
            this.DataContext = this;

            if (!string.IsNullOrEmpty(filePath))
            {
                Player.Open(filePath);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Player?.Dispose();
            base.OnClosed(e);
        }
    }
}
