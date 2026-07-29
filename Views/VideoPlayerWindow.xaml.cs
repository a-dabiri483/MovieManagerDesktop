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

            try
            {
                Engine.Start();
            }
            catch { }

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
