using System;
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

            if (Engine.Config == null)
            {
                Engine.Start(new EngineConfig()
                {
                    FFmpegPath = ":FFmpeg"
                });
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
