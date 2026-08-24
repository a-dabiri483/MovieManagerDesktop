using System;
using System.Drawing;
using System.Windows.Forms;

namespace MpvMenuHelper
{
    public class SyncForm : Form
    {
        private readonly MpvIpcClient _ipc;
        private double _subDelay = 0.0;
        private double _audioDelay = 0.0;

        private Label? _lblSubValue;
        private Label? _lblAudioValue;

        public SyncForm(string pipeName, double initialSubDelay = 0.0, double initialAudioDelay = 0.0)
        {
            _ipc = new MpvIpcClient(pipeName);
            _subDelay = initialSubDelay;
            _audioDelay = initialAudioDelay;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "همگام‌سازی صدا و زیرنویس";
            this.Size = new Size(380, 290);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(24, 24, 28);
            this.ForeColor = Color.White;
            this.RightToLeft = RightToLeft.Yes;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;

            // Header panel for dragging
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(32, 32, 38)
            };

            var lblTitle = new Label
            {
                Text = "⏱ همگام‌سازی صدا و زیرنویس",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 235, 240),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 0, 0, 0)
            };

            var btnClose = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 180, 190),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(40, 44),
                Dock = DockStyle.Left,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 40, 60);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 20, 40);
            btnClose.Click += (s, e) => this.Close();

            // Dragging logic
            bool dragging = false;
            Point dragStart = Point.Empty;
            lblTitle.MouseDown += (s, e) => { dragging = true; dragStart = new Point(e.X, e.Y); };
            lblTitle.MouseMove += (s, e) =>
            {
                if (dragging)
                {
                    Point diff = Point.Subtract(Cursor.Position, new Size(dragStart));
                    this.Location = diff;
                }
            };
            lblTitle.MouseUp += (s, e) => dragging = false;

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(btnClose);
            this.Controls.Add(pnlHeader);

            // Container Panel
            var pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16)
            };

            int top = 12;

            // 1. Subtitle Sync Section
            var lblSubSection = new Label
            {
                Text = "تاخیر زیرنویس (ثانیه):",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 80, 90),
                Location = new Point(16, top),
                AutoSize = true
            };
            pnlContent.Controls.Add(lblSubSection);
            top += 26;

            var pnlSubControls = new Panel { Location = new Point(16, top), Size = new Size(340, 42) };
            var btnSubMinus = CreateButton("- ۰.۵ ثانیه", 85, (s, e) => AdjustSubDelay(-0.5));
            btnSubMinus.Location = new Point(0, 4);

            _lblSubValue = new Label
            {
                Text = FormatDelay(_subDelay),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 215, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(80, 32),
                Location = new Point(90, 4),
                BackColor = Color.FromArgb(38, 38, 45)
            };

            var btnSubPlus = CreateButton("+ ۰.۵ ثانیه", 85, (s, e) => AdjustSubDelay(0.5));
            btnSubPlus.Location = new Point(175, 4);

            var btnSubReset = CreateButton("بازنشانی", 70, (s, e) => ResetSubDelay(), Color.FromArgb(60, 60, 70));
            btnSubReset.Location = new Point(265, 4);

            pnlSubControls.Controls.Add(btnSubMinus);
            pnlSubControls.Controls.Add(_lblSubValue);
            pnlSubControls.Controls.Add(btnSubPlus);
            pnlSubControls.Controls.Add(btnSubReset);
            pnlContent.Controls.Add(pnlSubControls);
            top += 54;

            // Separator
            var pnlDiv = new Panel
            {
                Location = new Point(16, top),
                Size = new Size(340, 1),
                BackColor = Color.FromArgb(45, 45, 55)
            };
            pnlContent.Controls.Add(pnlDiv);
            top += 14;

            // 2. Audio Sync Section
            var lblAudioSection = new Label
            {
                Text = "تاخیر صدا (ثانیه):",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 160, 240),
                Location = new Point(16, top),
                AutoSize = true
            };
            pnlContent.Controls.Add(lblAudioSection);
            top += 26;

            var pnlAudioControls = new Panel { Location = new Point(16, top), Size = new Size(340, 42) };
            var btnAudioMinus = CreateButton("- ۰.۱ ثانیه", 85, (s, e) => AdjustAudioDelay(-0.1));
            btnAudioMinus.Location = new Point(0, 4);

            _lblAudioValue = new Label
            {
                Text = FormatDelay(_audioDelay),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 200, 255),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(80, 32),
                Location = new Point(90, 4),
                BackColor = Color.FromArgb(38, 38, 45)
            };

            var btnAudioPlus = CreateButton("+ ۰.۱ ثانیه", 85, (s, e) => AdjustAudioDelay(0.1));
            btnAudioPlus.Location = new Point(175, 4);

            var btnAudioReset = CreateButton("بازنشانی", 70, (s, e) => ResetAudioDelay(), Color.FromArgb(60, 60, 70));
            btnAudioReset.Location = new Point(265, 4);

            pnlAudioControls.Controls.Add(btnAudioMinus);
            pnlAudioControls.Controls.Add(_lblAudioValue);
            pnlAudioControls.Controls.Add(btnAudioPlus);
            pnlAudioControls.Controls.Add(btnAudioReset);
            pnlContent.Controls.Add(pnlAudioControls);
            top += 54;

            this.Controls.Add(pnlContent);

            // Border painting
            this.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(220, 50, 65), 1.5f);
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            };
        }

        private Button CreateButton(string text, int width, EventHandler onClick, Color? bgColor = null)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = bgColor ?? Color.FromArgb(48, 48, 58),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(width, 32),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 50, 65);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(170, 30, 45);
            btn.Click += onClick;
            return btn;
        }

        private void AdjustSubDelay(double delta)
        {
            _subDelay = Math.Round(_subDelay + delta, 2);
            if (_lblSubValue != null) _lblSubValue.Text = FormatDelay(_subDelay);
            _ipc.SendCommand("add", "sub-delay", delta);
        }

        private void ResetSubDelay()
        {
            _subDelay = 0.0;
            if (_lblSubValue != null) _lblSubValue.Text = FormatDelay(0.0);
            _ipc.SendCommand("set_property", "sub-delay", 0.0);
            _ipc.SendCommand("set", "sub-delay", 0);
        }

        private void AdjustAudioDelay(double delta)
        {
            _audioDelay = Math.Round(_audioDelay + delta, 2);
            if (_lblAudioValue != null) _lblAudioValue.Text = FormatDelay(_audioDelay);
            _ipc.SendCommand("add", "audio-delay", delta);
        }

        private void ResetAudioDelay()
        {
            _audioDelay = 0.0;
            if (_lblAudioValue != null) _lblAudioValue.Text = FormatDelay(0.0);
            _ipc.SendCommand("set_property", "audio-delay", 0.0);
            _ipc.SendCommand("set", "audio-delay", 0);
        }

        private string FormatDelay(double delay)
        {
            if (Math.Abs(delay) < 0.001) return "۰.۰s";
            return (delay > 0 ? "+" : "") + delay.ToString("0.0") + "s";
        }
    }
}
