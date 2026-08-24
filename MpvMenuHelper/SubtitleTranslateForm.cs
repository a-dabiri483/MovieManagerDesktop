using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MpvMenuHelper
{
    public class SubtitleTranslateForm : Form
    {
        private readonly MpvIpcClient _ipc;
        private string _subFilePath;

        private TextBox _txtFilePath;
        private ProgressBar _progBar;
        private Label _lblStatus;
        private Button _btnTranslate;
        private CancellationTokenSource? _cts;

        public SubtitleTranslateForm(string pipeName, string currentSubPath = "")
        {
            _ipc = new MpvIpcClient(pipeName);
            _subFilePath = currentSubPath;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "ترجمه هوشمند زیرنویس";
            this.Size = new Size(460, 310);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(24, 24, 28);
            this.ForeColor = Color.White;
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
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
                Text = "🌐 ترجمه هوشمند زیرنویس به فارسی",
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
            btnClose.Click += (s, e) =>
            {
                _cts?.Cancel();
                this.Close();
            };

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

            var pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16)
            };

            int top = 12;

            var lblFile = new Label
            {
                Text = "مسیر فایل زیرنویس مبدا (SRT):",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 210),
                Location = new Point(16, top),
                AutoSize = true
            };
            pnlContent.Controls.Add(lblFile);
            top += 26;

            var pnlFilePick = new Panel { Location = new Point(16, top), Size = new Size(410, 36) };

            _txtFilePath = new TextBox
            {
                Text = _subFilePath,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.FromArgb(36, 36, 44),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(310, 28),
                Location = new Point(0, 4),
                RightToLeft = RightToLeft.No
            };

            var btnBrowse = new Button
            {
                Text = "انتخاب...",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(48, 48, 58),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(90, 28),
                Location = new Point(318, 4),
                Cursor = Cursors.Hand
            };
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 50, 65);
            btnBrowse.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "فایل‌های زیرنویس (*.srt;*.vtt)|*.srt;*.vtt|همه فایل‌ها (*.*)|*.*",
                    Title = "انتخاب فایل زیرنویس برای ترجمه"
                };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _txtFilePath.Text = ofd.FileName;
                    _subFilePath = ofd.FileName;
                }
            };

            pnlFilePick.Controls.Add(_txtFilePath);
            pnlFilePick.Controls.Add(btnBrowse);
            pnlContent.Controls.Add(pnlFilePick);
            top += 44;

            // Status Label
            _lblStatus = new Label
            {
                Text = "زیرنویس را انتخاب کنید و دکمه ترجمه را بزنید.",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(160, 160, 175),
                Location = new Point(16, top),
                Size = new Size(410, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlContent.Controls.Add(_lblStatus);
            top += 26;

            // Progress Bar
            _progBar = new ProgressBar
            {
                Location = new Point(16, top),
                Size = new Size(410, 18),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            pnlContent.Controls.Add(_progBar);
            top += 30;

            // Actions panel
            var pnlActions = new Panel { Location = new Point(16, top), Size = new Size(410, 42) };

            _btnTranslate = new Button
            {
                Text = "⚡ ترجمه و اعمال فوری روی ویدیو",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(220, 50, 65),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(240, 36),
                Location = new Point(0, 2),
                Cursor = Cursors.Hand
            };
            _btnTranslate.FlatAppearance.BorderSize = 0;
            _btnTranslate.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 70, 85);
            _btnTranslate.Click += async (s, e) => await StartTranslationAsync();

            var btnCancel = new Button
            {
                Text = "بستن",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 60),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 36),
                Location = new Point(250, 2),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) =>
            {
                _cts?.Cancel();
                this.Close();
            };

            pnlActions.Controls.Add(_btnTranslate);
            pnlActions.Controls.Add(btnCancel);
            pnlContent.Controls.Add(pnlActions);

            this.Controls.Add(pnlContent);

            // Border painting
            this.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(220, 50, 65), 1.5f);
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            };
        }

        private async Task StartTranslationAsync()
        {
            string targetPath = _txtFilePath.Text.Trim();
            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
            {
                MessageBox.Show("لطفاً ابتدا یک فایل زیرنویس معتبر انتخاب کنید.", "خطا", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnTranslate.Enabled = false;
            _progBar.Value = 0;
            _cts = new CancellationTokenSource();

            var progress = new Progress<(int current, int total, string status)>(p =>
            {
                int percent = (int)((double)p.current / p.total * 100);
                _progBar.Value = Math.Min(100, Math.Max(0, percent));
                _lblStatus.Text = $"{p.status} ({percent}%)";
            });

            try
            {
                _lblStatus.Text = "در حال شروع ترجمه خط به خط...";
                string translatedFile = await Task.Run(() => SubtitleTranslator.TranslateSrtFileAsync(targetPath, "fa", progress, _cts.Token));

                _progBar.Value = 100;
                _lblStatus.ForeColor = Color.FromArgb(80, 220, 120);
                _lblStatus.Text = "✅ ترجمه با موفقیت تکمیل شد و روی پلیر اعمال گشت!";

                // Apply to MPV
                _ipc.SendCommand("sub-add", translatedFile, "select");
            }
            catch (OperationCanceledException)
            {
                _lblStatus.Text = "عملیات ترجمه لغو شد.";
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = Color.FromArgb(240, 80, 90);
                _lblStatus.Text = "❌ خطا در ترجمه: " + ex.Message;
            }
            finally
            {
                _btnTranslate.Enabled = true;
            }
        }
    }
}
