using System;
using System.IO;
using System.Windows;
using FlyleafLib;
using FlyleafLib.MediaPlayer;

namespace MovieManagerDesktop.Views
{
    public partial class VideoPlayerWindow : Window
    {
        public Player Player { get; set; }

        public VideoPlayerWindow(string filePath)
        {
            InitializeComponent();

            string ffmpegFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FFmpeg");

            if (Engine.Config == null)
            {
                try
                {
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
