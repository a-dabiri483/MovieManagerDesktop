using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MpvMenuHelper
{
    public class SubtitleDownloadForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private readonly string _pipeName;
        private readonly string _videoPath;
        private readonly MpvIpcClient _ipc;

        private TextBox txtSearch = null!;
        private ComboBox cmbLanguage = null!;
        private Button btnSearch = null!;
        private ListView lvSubtitles = null!;
        private Label lblStatus = null!;

        private readonly List<SubResultItem> _searchResults = new();
        private int _hoveredRowIndex = -1;
        private int _hoveredColIndex = -1;
        private bool _isDownloading = false;

        public SubtitleDownloadForm(string pipeName, string videoPath)
        {
            _pipeName = pipeName;
            _videoPath = videoPath;
            _ipc = new MpvIpcClient(pipeName);

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            InitializeComponent();
            ApplyDarkTheme();

            string initialQuery = ExtractQueryFromPath(videoPath);
            txtSearch.Text = initialQuery;

            this.Load += async (s, e) =>
            {
                AdjustListViewColumns();
                if (!string.IsNullOrWhiteSpace(initialQuery))
                {
                    await PerformSearchAsync();
                }
            };

            this.Resize += (s, e) => AdjustListViewColumns();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int useDarkMode = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref useDarkMode, sizeof(int));
                DwmSetWindowAttribute(this.Handle, 19, ref useDarkMode, sizeof(int));

                int cornerPref = 2;
                DwmSetWindowAttribute(this.Handle, 33, ref cornerPref, sizeof(int));
            }
            catch { }
        }

        public static (string cleanTitle, int? season, int? episode) ParseSearchQuery(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return ("", null, null);

            int? season = null;
            int? episode = null;

            // Match S01E01, S1E1, 1x01, Season 1 Episode 1, فصل 1 قسمت 1
            var sEpMatch = Regex.Match(input, @"(?:[sS](\d+)\s*[eE](\d+)|(?:(\d+)x(\d+))|(?:فصل\s*(\d+)\s*قسمت\s*(\d+))|(?:Season\s*(\d+)\s*Episode\s*(\d+)))", RegexOptions.IgnoreCase);
            if (sEpMatch.Success)
            {
                if (int.TryParse(sEpMatch.Groups[1].Value, out int s)) season = s;
                else if (int.TryParse(sEpMatch.Groups[3].Value, out int s2)) season = s2;
                else if (int.TryParse(sEpMatch.Groups[5].Value, out int s3)) season = s3;
                else if (int.TryParse(sEpMatch.Groups[7].Value, out int s4)) season = s4;

                if (int.TryParse(sEpMatch.Groups[2].Value, out int e)) episode = e;
                else if (int.TryParse(sEpMatch.Groups[4].Value, out int e2)) episode = e2;
                else if (int.TryParse(sEpMatch.Groups[6].Value, out int e3)) episode = e3;
                else if (int.TryParse(sEpMatch.Groups[8].Value, out int e4)) episode = e4;
            }
            else
            {
                var sOnly = Regex.Match(input, @"(?:[sS]|Season\s*|فصل\s*)(\d+)", RegexOptions.IgnoreCase);
                if (sOnly.Success && int.TryParse(sOnly.Groups[1].Value, out int sVal)) season = sVal;

                var eOnly = Regex.Match(input, @"(?:[eE]|Episode\s*|قسمت\s*)(\d+)", RegexOptions.IgnoreCase);
                if (eOnly.Success && int.TryParse(eOnly.Groups[1].Value, out int eVal)) episode = eVal;
            }

            // Strip video quality, codecs, release groups, resolutions (720, 1080, 480, 2160, 4k)
            string cleaned = Regex.Replace(input, @"(?i)\b(?:1080p?|720p?|480p?|2160p?|576p?|4k|uhd|bluray|bdrip|brrip|web-?dl|webrip|web|hdtv|dvdrip|x264|x265|hevc|h264|h265|aac|dts|ac3|yify|pahe|psa|rarbg|eztv|galaxytv|amzn|nf|dsnp|proper|repack|remux|hdr|10bit|60fps|dual-audio|dubbed|farsi|persian|sub|softsub)\b", " ");
            cleaned = Regex.Replace(cleaned, @"(?i)\b(?:720|1080|2160|480|576)\b", " ");
            cleaned = Regex.Replace(cleaned, @"(?i)(?:[sS]\d+\s*[eE]\d+|\d+x\d+|Season\s*\d+|Episode\s*\d+|فصل\s*\d+|قسمت\s*\d+)", " ");
            cleaned = Regex.Replace(cleaned, @"[\.\[\]\(\)\-_]", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            return (cleaned, season, episode);
        }

        private string ExtractQueryFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            string name = Path.GetFileNameWithoutExtension(path);
            var (clean, season, episode) = ParseSearchQuery(name);
            if (season != null && episode != null)
                return $"{clean} S{season:D2}E{episode:D2}".Trim();
            if (season != null)
                return $"{clean} S{season:D2}".Trim();
            return !string.IsNullOrWhiteSpace(clean) ? clean : name;
        }

        private void AdjustListViewColumns()
        {
            if (lvSubtitles != null && lvSubtitles.Columns.Count >= 4)
            {
                int totalWidth = lvSubtitles.ClientSize.Width;
                int col0 = 100; // Language
                int col1 = 100; // Download Button
                int col3 = 110; // Source
                int col2 = Math.Max(220, totalWidth - col0 - col1 - col3 - 4); // Title fills remainder

                lvSubtitles.Columns[0].Width = col0;
                lvSubtitles.Columns[1].Width = col1;
                lvSubtitles.Columns[2].Width = col2;
                lvSubtitles.Columns[3].Width = col3;
            }
        }

        private void InitializeComponent()
        {
            this.Text = "دانلود آنلاین زیرنویس (SubDL & SubSource & OpenSubtitles)";
            this.Size = new Size(860, 560);
            this.MinimumSize = new Size(720, 460);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Padding = new Padding(16, 14, 16, 14);

            // ==========================================
            // 1. Top Search Bar Panel (Right: Search Btn | Center: Input | Left: Language Dropdown)
            // ==========================================
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(0, 0, 0, 8)
            };

            // Search Button (Dock Right -> in RTL appears on right side)
            btnSearch = new Button
            {
                Text = "🔍 جستجو",
                Dock = DockStyle.Right,
                Width = 105,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(235, 59, 90),
                ForeColor = Color.White
            };
            btnSearch.FlatAppearance.BorderSize = 0;
            btnSearch.Click += async (s, e) => await PerformSearchAsync();

            // Language Dropdown (Dock Left -> in RTL appears on left side)
            cmbLanguage = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 145,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 26,
                FlatStyle = FlatStyle.Popup
            };
            cmbLanguage.Items.AddRange(new object[] { "🇮🇷 همه زبان‌ها", "🇮🇷 فقط فارسی", "🇬🇧 فقط انگلیسی" });
            cmbLanguage.SelectedIndex = 0;
            cmbLanguage.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool isSelected = (e.State & DrawItemState.Selected) != 0;
                var bg = isSelected ? Color.FromArgb(56, 103, 214) : Color.FromArgb(32, 32, 48);
                var fg = Color.White;

                using var brush = new SolidBrush(bg);
                e.Graphics.FillRectangle(brush, e.Bounds);

                string text = cmbLanguage.Items[e.Index].ToString() ?? "";
                TextRenderer.DrawText(e.Graphics, text, cmbLanguage.Font, e.Bounds, fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.RightToLeft);
            };

            var pnlSearchBoxWrap = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 0, 10, 0)
            };

            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearch.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await PerformSearchAsync();
                }
            };
            pnlSearchBoxWrap.Controls.Add(txtSearch);

            pnlTop.Controls.Add(pnlSearchBoxWrap);
            pnlTop.Controls.Add(cmbLanguage);
            pnlTop.Controls.Add(btnSearch);

            // ==========================================
            // 2. Status Bar (Dock Bottom)
            // ==========================================
            lblStatus = new Label
            {
                Text = "برای دانلود، روی دکمه «دانلود» هر زیرنویس یا ردیف آن کلیک کنید.",
                Dock = DockStyle.Bottom,
                Height = 32,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(164, 176, 190),
                Font = new Font("Segoe UI", 9.5f),
                Padding = new Padding(0, 6, 4, 0)
            };

            // ==========================================
            // 3. Custom OwnerDraw ListView with In-Row Download Buttons (Dock Fill)
            // ==========================================
            lvSubtitles = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                MultiSelect = false,
                Font = new Font("Segoe UI", 10f),
                OwnerDraw = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            lvSubtitles.Columns.Add("زبان", 100, HorizontalAlignment.Center);
            lvSubtitles.Columns.Add("دانلود", 100, HorizontalAlignment.Center);
            lvSubtitles.Columns.Add("عنوان و مشخصات نسخه انتشار", 530, HorizontalAlignment.Right);
            lvSubtitles.Columns.Add("منبع", 110, HorizontalAlignment.Center);

            // Column Header Owner Draw
            lvSubtitles.DrawColumnHeader += (s, e) =>
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(36, 36, 56)))
                {
                    e.Graphics.FillRectangle(bgBrush, e.Bounds);
                }

                using (var pen = new Pen(Color.FromArgb(235, 59, 90), 2))
                {
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }

                using (var sepPen = new Pen(Color.FromArgb(48, 48, 72), 1))
                {
                    e.Graphics.DrawLine(sepPen, e.Bounds.Left, e.Bounds.Top + 4, e.Bounds.Left, e.Bounds.Bottom - 6);
                }

                using var font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                TextRenderer.DrawText(
                    e.Graphics,
                    e.Header?.Text ?? "",
                    font,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
                    Color.FromArgb(220, 224, 230),
                    TextFormatFlags.VerticalCenter | (e.Header?.TextAlign == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Right) | TextFormatFlags.RightToLeft
                );
            };

            // Item Owner Draw
            lvSubtitles.DrawItem += (s, e) => { e.DrawDefault = false; };

            // SubItem Owner Draw
            lvSubtitles.DrawSubItem += (s, e) =>
            {
                bool isSelected = (e.ItemState & ListViewItemStates.Selected) != 0;
                var rowBg = isSelected ? Color.FromArgb(44, 48, 76) : (e.ItemIndex % 2 == 0 ? Color.FromArgb(20, 20, 32) : Color.FromArgb(25, 25, 40));

                using (var bgBrush = new SolidBrush(rowBg))
                {
                    e.Graphics.FillRectangle(bgBrush, e.Bounds);
                }

                using (var sepPen = new Pen(Color.FromArgb(34, 34, 52), 1))
                {
                    e.Graphics.DrawLine(sepPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }

                if (isSelected && e.ColumnIndex == 0)
                {
                    using var indBrush = new SolidBrush(Color.FromArgb(235, 59, 90));
                    e.Graphics.FillRectangle(indBrush, e.Bounds.Right - 4, e.Bounds.Top, 4, e.Bounds.Height);
                }

                string text = e.SubItem?.Text ?? "";

                if (e.ColumnIndex == 0) // Column 0: Language Badge Pill
                {
                    bool isFa = text.Contains("فارسی");
                    Color badgeBg = isFa ? Color.FromArgb(22, 54, 38) : Color.FromArgb(24, 44, 76);
                    Color badgeFg = isFa ? Color.FromArgb(46, 213, 115) : Color.FromArgb(112, 161, 255);
                    Color badgeBorder = isFa ? Color.FromArgb(46, 213, 115, 80) : Color.FromArgb(112, 161, 255, 80);

                    var pillRect = new Rectangle(e.Bounds.X + (e.Bounds.Width - 85) / 2, e.Bounds.Y + 3, 85, e.Bounds.Height - 7);
                    using var path = GetRoundedRectangle(pillRect, 6);
                    using var fill = new SolidBrush(badgeBg);
                    using var stroke = new Pen(badgeBorder, 1);

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(stroke, path);
                    e.Graphics.SmoothingMode = SmoothingMode.Default;

                    using var badgeFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    TextRenderer.DrawText(e.Graphics, text, badgeFont, pillRect, badgeFg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else if (e.ColumnIndex == 1) // Column 1: In-Row Download Button Pill
                {
                    bool isBtnHovered = (_hoveredRowIndex == e.ItemIndex && _hoveredColIndex == 1);
                    Color btnBg = isBtnHovered ? Color.FromArgb(5, 150, 105) : Color.FromArgb(16, 185, 129);
                    Color btnFg = Color.White;

                    var btnRect = new Rectangle(e.Bounds.X + (e.Bounds.Width - 82) / 2, e.Bounds.Y + 3, 82, e.Bounds.Height - 7);
                    using var path = GetRoundedRectangle(btnRect, 5);
                    using var fill = new SolidBrush(btnBg);

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.SmoothingMode = SmoothingMode.Default;

                    using var btnFont = new Font("Segoe UI", 9f, FontStyle.Bold);
                    TextRenderer.DrawText(e.Graphics, "⬇️ دانلود", btnFont, btnRect, btnFg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else if (e.ColumnIndex == 3) // Column 3: Source Badge
                {
                    bool isSubdl = text.Contains("SubDL");
                    bool isSubSource = text.Contains("SubSource");
                    Color srcBg = isSubdl ? Color.FromArgb(44, 32, 20) : (isSubSource ? Color.FromArgb(36, 24, 52) : Color.FromArgb(20, 36, 48));
                    Color srcFg = isSubdl ? Color.FromArgb(255, 165, 2) : (isSubSource ? Color.FromArgb(165, 94, 234) : Color.FromArgb(56, 189, 248));

                    var srcRect = new Rectangle(e.Bounds.X + (e.Bounds.Width - 90) / 2, e.Bounds.Y + 4, 90, e.Bounds.Height - 9);
                    using var path = GetRoundedRectangle(srcRect, 5);
                    using var fill = new SolidBrush(srcBg);

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.SmoothingMode = SmoothingMode.Default;

                    using var srcFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    TextRenderer.DrawText(e.Graphics, text, srcFont, srcRect, srcFg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else // Column 2: Title & Release Info
                {
                    Color textFg = isSelected ? Color.White : Color.FromArgb(240, 242, 245);
                    using var rowFont = new Font("Segoe UI", 9.5f, isSelected ? FontStyle.Bold : FontStyle.Regular);
                    var textRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, text, rowFont, textRect, textFg, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.RightToLeft);
                }
            };

            // Interactive mouse events for in-row download buttons
            lvSubtitles.MouseMove += (s, e) =>
            {
                var hit = lvSubtitles.HitTest(e.Location);
                if (hit.Item != null && hit.SubItem != null)
                {
                    int colIndex = hit.Item.SubItems.IndexOf(hit.SubItem);
                    if (colIndex == 1) // On Download button
                    {
                        lvSubtitles.Cursor = Cursors.Hand;
                        if (_hoveredRowIndex != hit.Item.Index || _hoveredColIndex != 1)
                        {
                            _hoveredRowIndex = hit.Item.Index;
                            _hoveredColIndex = 1;
                            lvSubtitles.Invalidate(hit.Item.Bounds);
                        }
                        return;
                    }
                }

                lvSubtitles.Cursor = Cursors.Default;
                if (_hoveredRowIndex != -1 || _hoveredColIndex != -1)
                {
                    _hoveredRowIndex = -1;
                    _hoveredColIndex = -1;
                    lvSubtitles.Invalidate();
                }
            };

            lvSubtitles.MouseLeave += (s, e) =>
            {
                lvSubtitles.Cursor = Cursors.Default;
                if (_hoveredRowIndex != -1 || _hoveredColIndex != -1)
                {
                    _hoveredRowIndex = -1;
                    _hoveredColIndex = -1;
                    lvSubtitles.Invalidate();
                }
            };

            lvSubtitles.MouseClick += async (s, e) =>
            {
                if (_isDownloading) return;
                var hit = lvSubtitles.HitTest(e.Location);
                if (hit.Item != null)
                {
                    int colIndex = hit.SubItem != null ? hit.Item.SubItems.IndexOf(hit.SubItem) : -1;
                    if (colIndex == 1 && hit.Item.Tag is SubResultItem item)
                    {
                        await PerformDownloadItemAsync(item);
                    }
                }
            };

            lvSubtitles.DoubleClick += async (s, e) =>
            {
                if (_isDownloading) return;
                if (lvSubtitles.SelectedItems.Count > 0 && lvSubtitles.SelectedItems[0].Tag is SubResultItem item)
                {
                    await PerformDownloadItemAsync(item);
                }
            };

            this.Controls.Add(lvSubtitles);
            this.Controls.Add(lblStatus);
            this.Controls.Add(pnlTop);
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ApplyDarkTheme()
        {
            this.BackColor = Color.FromArgb(20, 20, 32);
            this.ForeColor = Color.White;

            txtSearch.BackColor = Color.FromArgb(28, 28, 44);
            txtSearch.ForeColor = Color.White;

            cmbLanguage.BackColor = Color.FromArgb(28, 28, 44);
            cmbLanguage.ForeColor = Color.White;

            btnSearch.BackColor = Color.FromArgb(235, 59, 90);
            btnSearch.ForeColor = Color.White;

            lvSubtitles.BackColor = Color.FromArgb(16, 16, 26);
            lvSubtitles.ForeColor = Color.White;
        }

        private async Task PerformSearchAsync()
        {
            string query = txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            btnSearch.Enabled = false;
            lblStatus.Text = "⏳ در حال جستجو در پایگاه‌های زیرنویس (SubDL & SubSource & OpenSubtitles)...";
            lblStatus.ForeColor = Color.FromArgb(56, 189, 248);
            lvSubtitles.Items.Clear();
            _searchResults.Clear();

            string langFilter = cmbLanguage.SelectedIndex switch
            {
                1 => "FA",
                2 => "EN",
                _ => "ALL"
            };

            try
            {
                var items = await SearchOnlineSubtitlesInternalAsync(query, langFilter);
                _searchResults.AddRange(items);

                foreach (var item in _searchResults)
                {
                    var lvi = new ListViewItem(item.Language);
                    lvi.SubItems.Add("⬇️ دانلود"); // Column 1
                    lvi.SubItems.Add(item.Title);     // Column 2
                    lvi.SubItems.Add(item.Source);    // Column 3
                    lvi.Tag = item;
                    lvSubtitles.Items.Add(lvi);
                }

                if (_searchResults.Count > 0)
                {
                    lblStatus.Text = $"✔ {_searchResults.Count} زیرنویس معتبر یافت شد. برای دانلود و الصاق خودکار به فیلم، روی دکمه «دانلود» کلیک کنید.";
                    lblStatus.ForeColor = Color.FromArgb(46, 213, 115);
                    lvSubtitles.Items[0].Selected = true;
                }
                else
                {
                    lblStatus.Text = "❌ زیرنویسی برای این عبارت یافت نشد. لطفاً نام انگلیسی فیلم/سریال را دقیق‌تر وارد کنید.";
                    lblStatus.ForeColor = Color.FromArgb(255, 71, 87);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"⚠️ خطا در ارتباط با سرورهای زیرنویس: {ex.Message}";
                lblStatus.ForeColor = Color.FromArgb(255, 71, 87);
            }
            finally
            {
                btnSearch.Enabled = true;
                AdjustListViewColumns();
            }
        }

        private async Task PerformDownloadItemAsync(SubResultItem item)
        {
            if (_isDownloading) return;
            _isDownloading = true;

            btnSearch.Enabled = false;
            lblStatus.Text = "⏳ در حال دانلود، رفع انکودینگ کاراکترهای فارسی و فعال‌سازی فوری روی پلیر...";
            lblStatus.ForeColor = Color.FromArgb(56, 189, 248);

            try
            {
                string savedSrt = await DownloadAndSaveSubtitleInternalAsync(item, _videoPath);
                
                // Activate in MPV via IPC
                await _ipc.SendCommandAsync("sub-add", savedSrt, "select");
                await _ipc.SendCommandAsync("set", "sub-visibility", "yes");
                await _ipc.SendCommandAsync("show-text", $"زیرنویس فعال شد: {Path.GetFileName(savedSrt)}", 3000);

                lblStatus.Text = $"✅ زیرنویس با موفقیت دانلود و روی فیلم فعال شد: {Path.GetFileName(savedSrt)}";
                lblStatus.ForeColor = Color.FromArgb(46, 213, 115);

                await Task.Delay(1100);
                this.Close();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ خطا در دانلود و استخراج فایل زیرنویس: {ex.Message}";
                lblStatus.ForeColor = Color.FromArgb(255, 71, 87);
                btnSearch.Enabled = true;
                _isDownloading = false;
            }
        }

        // --- Network Helpers ---
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
        private const string SUBDL_KEY = "subdl_HHtBliLNdNumqWs29n7Z4E9GLQwyX0bL9MDFc6RTy34";
        private const string SUBSOURCE_KEY = "sk_68d68b32ef82a0a168e243815c66d85ca5ecfe2909507245e8ff695b27c10025";
        private const string OPENSUBTITLES_KEY = "tf6Ebu6rUqT662SZlDWYWw5yJkS9Gz2g";

        private async Task<List<SubResultItem>> SearchOnlineSubtitlesInternalAsync(string rawQuery, string langFilter)
        {
            var results = new List<SubResultItem>();
            var (cleanTitle, season, episode) = ParseSearchQuery(rawQuery);
            if (string.IsNullOrWhiteSpace(cleanTitle)) cleanTitle = rawQuery.Trim();

            string subdlLangs = langFilter == "EN" ? "EN" : (langFilter == "FA" ? "FA" : "FA,EN");

            // ==========================================
            // 1. SubDL (Multi-tier: Exact Ep -> Season -> Title)
            // ==========================================
            try
            {
                var subdlQueries = new List<string>();
                string encTitle = Uri.EscapeDataString(cleanTitle);

                if (season != null && episode != null)
                {
                    subdlQueries.Add($"https://api.subdl.com/api/v1/subtitles?film_name={encTitle}&type=tv&season_number={season}&episode_number={episode}&languages={subdlLangs}&api_key={SUBDL_KEY}&subs_per_page=30");
                }
                if (season != null)
                {
                    subdlQueries.Add($"https://api.subdl.com/api/v1/subtitles?film_name={encTitle}&type=tv&season_number={season}&languages={subdlLangs}&api_key={SUBDL_KEY}&subs_per_page=30");
                }
                subdlQueries.Add($"https://api.subdl.com/api/v1/subtitles?film_name={encTitle}&languages={subdlLangs}&api_key={SUBDL_KEY}&subs_per_page=30");

                foreach (var url in subdlQueries)
                {
                    if (results.Count >= 25) break;

                    try
                    {
                        var resp = await _http.GetAsync(url);
                        if (!resp.IsSuccessStatusCode) continue;

                        string json = await resp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("status", out var st) && st.GetBoolean() &&
                            root.TryGetProperty("subtitles", out var subsArr) && subsArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var sub in subsArr.EnumerateArray())
                            {
                                string rel = sub.TryGetProperty("release_name", out var rn) ? rn.GetString() ?? "" : "";
                                string rawLang = sub.TryGetProperty("lang", out var l) ? l.GetString() ?? "FA" : "FA";
                                string u = sub.TryGetProperty("url", out var up) ? up.GetString() ?? "" : "";
                                if (string.IsNullOrEmpty(u)) continue;

                                string fullUrl = u.StartsWith("http") ? u : $"https://dl.subdl.com{u}";
                                if (results.Any(r => r.DownloadUrl == fullUrl)) continue;

                                int? epNum = sub.TryGetProperty("episode", out var epP) && epP.ValueKind == JsonValueKind.Number ? epP.GetInt32() : null;
                                int? sNum = sub.TryGetProperty("season", out var sP) && sP.ValueKind == JsonValueKind.Number ? sP.GetInt32() : null;

                                bool isPersian = rawLang.Contains("fa", StringComparison.OrdinalIgnoreCase) ||
                                                 rawLang.Contains("farsi", StringComparison.OrdinalIgnoreCase) ||
                                                 rawLang.Contains("persian", StringComparison.OrdinalIgnoreCase);

                                string langLabel = isPersian ? "🇮🇷 فارسی" : (rawLang.Contains("en", StringComparison.OrdinalIgnoreCase) ? "🇬🇧 English" : rawLang);

                                results.Add(new SubResultItem
                                {
                                    Title = string.IsNullOrWhiteSpace(rel) ? cleanTitle : rel,
                                    Language = langLabel,
                                    LanguageCode = isPersian ? "fa" : "en",
                                    DownloadUrl = fullUrl,
                                    Source = "SubDL",
                                    Season = sNum,
                                    Episode = epNum,
                                    ReleaseInfo = rel
                                });
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // ==========================================
            // 2. SubSource
            // ==========================================
            if (results.Count < 20)
            {
                try
                {
                    string ssLang = langFilter == "EN" ? "english" : "persian";
                    string sUrl = $"https://api.subsource.net/api/v1/movies/search?searchType=text&q={Uri.EscapeDataString(cleanTitle)}";
                    using var req = new HttpRequestMessage(HttpMethod.Get, sUrl);
                    req.Headers.Add("X-API-Key", SUBSOURCE_KEY);
                    req.Headers.Add("Accept", "application/json");

                    var sResp = await _http.SendAsync(req);
                    if (sResp.IsSuccessStatusCode)
                    {
                        string sJson = await sResp.Content.ReadAsStringAsync();
                        using var sDoc = JsonDocument.Parse(sJson);
                        if (sDoc.RootElement.TryGetProperty("data", out var mArr) && mArr.ValueKind == JsonValueKind.Array)
                        {
                            var firstM = mArr.EnumerateArray().FirstOrDefault();
                            if (firstM.ValueKind == JsonValueKind.Object && firstM.TryGetProperty("movieId", out var mId))
                            {
                                int id = mId.GetInt32();
                                string subUrl = $"https://api.subsource.net/api/v1/subtitles?movieId={id}&language={ssLang}";
                                using var subReq = new HttpRequestMessage(HttpMethod.Get, subUrl);
                                subReq.Headers.Add("X-API-Key", SUBSOURCE_KEY);
                                subReq.Headers.Add("Accept", "application/json");

                                var subResp = await _http.SendAsync(subReq);
                                if (subResp.IsSuccessStatusCode)
                                {
                                    string subJson = await subResp.Content.ReadAsStringAsync();
                                    using var subDoc = JsonDocument.Parse(subJson);
                                    if (subDoc.RootElement.TryGetProperty("data", out var subItems) && subItems.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var sItem in subItems.EnumerateArray().Take(20))
                                        {
                                            int sid = sItem.TryGetProperty("subtitleId", out var sp) ? sp.GetInt32() : -1;
                                            if (sid <= 0) continue;
                                            string rel = "SubSource Subtitle";
                                            if (sItem.TryGetProperty("releaseInfo", out var relArr) && relArr.ValueKind == JsonValueKind.Array)
                                            {
                                                var firstRel = relArr.EnumerateArray().FirstOrDefault();
                                                if (firstRel.ValueKind == JsonValueKind.String)
                                                    rel = firstRel.GetString() ?? rel;
                                            }

                                            string dl = $"https://api.subsource.net/api/v1/subtitles/{sid}/download";
                                            string langLabel = ssLang == "persian" ? "🇮🇷 فارسی" : "🇬🇧 English";

                                            if (results.All(r => r.DownloadUrl != dl))
                                            {
                                                results.Add(new SubResultItem
                                                {
                                                    Title = rel,
                                                    Language = langLabel,
                                                    LanguageCode = ssLang == "persian" ? "fa" : "en",
                                                    DownloadUrl = dl,
                                                    Source = "SubSource",
                                                    ReleaseInfo = rel,
                                                    Season = season,
                                                    Episode = episode
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // ==========================================
            // 3. OpenSubtitles API
            // ==========================================
            if (results.Count < 20)
            {
                try
                {
                    string osLang = langFilter == "FA" ? "fa" : (langFilter == "EN" ? "en" : "fa,en");
                    string osUrl = $"https://api.opensubtitles.com/api/v1/subtitles?query={Uri.EscapeDataString(cleanTitle)}&languages={osLang}";
                    if (season != null) osUrl += $"&season_number={season}";
                    if (episode != null) osUrl += $"&episode_number={episode}";

                    using var osReq = new HttpRequestMessage(HttpMethod.Get, osUrl);
                    osReq.Headers.Add("Api-Key", OPENSUBTITLES_KEY);
                    osReq.Headers.Add("User-Agent", "MovieManagerDesktop v2.5");

                    var osResp = await _http.SendAsync(osReq);
                    if (osResp.IsSuccessStatusCode)
                    {
                        string osJson = await osResp.Content.ReadAsStringAsync();
                        using var osDoc = JsonDocument.Parse(osJson);
                        if (osDoc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var osItem in dataArr.EnumerateArray().Take(15))
                            {
                                if (osItem.TryGetProperty("attributes", out var attr))
                                {
                                    string rawLang = attr.TryGetProperty("language", out var l) ? l.GetString() ?? "fa" : "fa";
                                    string releaseName = attr.TryGetProperty("release", out var r) ? r.GetString() ?? "" : "";

                                    int fileId = -1;
                                    if (attr.TryGetProperty("files", out var filesArr) && filesArr.ValueKind == JsonValueKind.Array && filesArr.GetArrayLength() > 0)
                                    {
                                        fileId = filesArr[0].TryGetProperty("file_id", out var fId) ? fId.GetInt32() : -1;
                                    }

                                    if (fileId > 0)
                                    {
                                        bool isPersian = rawLang.Contains("fa", StringComparison.OrdinalIgnoreCase) ||
                                                         rawLang.Contains("farsi", StringComparison.OrdinalIgnoreCase) ||
                                                         rawLang.Contains("persian", StringComparison.OrdinalIgnoreCase);

                                        string langLabel = isPersian ? "🇮🇷 فارسی" : "🇬🇧 English";
                                        string titleText = string.IsNullOrWhiteSpace(releaseName) ? cleanTitle : releaseName;

                                        results.Add(new SubResultItem
                                        {
                                            Title = titleText,
                                            Language = langLabel,
                                            LanguageCode = isPersian ? "fa" : "en",
                                            DownloadUrl = $"https://api.opensubtitles.com/api/v1/download",
                                            OpenSubFileId = fileId,
                                            Source = "OpenSubtitles",
                                            Season = season,
                                            Episode = episode,
                                            ReleaseInfo = releaseName
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Intelligently rank results: Exact Season & Episode first!
            return results
                .OrderByDescending(r => CalculateMatchScore(r, season, episode))
                .ToList();
        }

        private static int CalculateMatchScore(SubResultItem item, int? targetSeason, int? targetEpisode)
        {
            int score = 0;
            string text = $"{item.Title} {item.ReleaseInfo}";

            // Language: Persian gets top priority
            if (item.Language.Contains("فارسی") || item.LanguageCode.Contains("fa"))
                score += 1000;

            if (targetSeason.HasValue && targetEpisode.HasValue)
            {
                int s = targetSeason.Value;
                int e = targetEpisode.Value;

                // 1. Structured S/E
                if (item.Season == s && item.Episode == e)
                {
                    score += 600; // Perfect match
                }
                else if (item.Season.HasValue && item.Season.Value != s)
                {
                    score -= 500; // Penalize wrong seasons (e.g. S22 vs S01)
                }

                // 2. Exact Episode Regex in title (e.g. S01E02, 1x02, 1x2, S1E2, 1-02)
                string exactPatterns = $@"(?i)\b(?:s0?{s}e0?{e}|{s}x0?{e}|s0?{s}\s*-\s*0?{e}|season\s*0?{s}\s*episode\s*0?{e})\b";
                if (Regex.IsMatch(text, exactPatterns))
                {
                    score += 700; // Definite exact title match
                }

                // Penalize wrong seasons found in title text
                var seasonMatch = Regex.Match(text, @"(?i)\b(?:s(\d+)|(\d+)x)\b");
                if (seasonMatch.Success)
                {
                    int foundSeason = -1;
                    if (seasonMatch.Groups[1].Success) int.TryParse(seasonMatch.Groups[1].Value, out foundSeason);
                    else if (seasonMatch.Groups[2].Success) int.TryParse(seasonMatch.Groups[2].Value, out foundSeason);

                    if (foundSeason > 0 && foundSeason != s)
                    {
                        score -= 400; // Severe penalty for wrong season in title
                    }
                }

                // Target Season Pack (e.g. Season 1 All Episodes)
                string packPattern = $@"(?i)\b(?:season\s*0?{s}|s0?{s}\b|all\s*episodes|complete)";
                if (Regex.IsMatch(text, packPattern) && !Regex.IsMatch(text, $@"(?i)\b(?:s0?[^{s}]|season\s*0?[^{s}])\b"))
                {
                    score += 250;
                }
            }
            else if (targetSeason.HasValue)
            {
                int s = targetSeason.Value;
                if (item.Season == s || Regex.IsMatch(text, $@"(?i)\b(?:s0?{s}|season\s*0?{s})\b"))
                    score += 300;
                else if (item.Season.HasValue && item.Season.Value != s)
                    score -= 400;
            }

            return score;
        }

        private async Task<string> DownloadAndSaveSubtitleInternalAsync(SubResultItem item, string targetVideoPath)
        {
            string dir = Path.GetDirectoryName(targetVideoPath)!;
            string baseName = Path.GetFileNameWithoutExtension(targetVideoPath);
            string targetPath = Path.Combine(dir, $"{baseName}.{(item.LanguageCode.Contains("fa") ? "fa" : item.LanguageCode)}.srt");

            byte[] raw;

            if (item.Source == "OpenSubtitles" && item.OpenSubFileId.HasValue)
            {
                using var osReq = new HttpRequestMessage(HttpMethod.Post, "https://api.opensubtitles.com/api/v1/download");
                osReq.Headers.Add("Api-Key", OPENSUBTITLES_KEY);
                osReq.Headers.Add("User-Agent", "MovieManagerDesktop v2.5");
                osReq.Content = new StringContent(JsonSerializer.Serialize(new { file_id = item.OpenSubFileId.Value }), Encoding.UTF8, "application/json");

                var osResp = await _http.SendAsync(osReq);
                osResp.EnsureSuccessStatusCode();

                string osJson = await osResp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(osJson);
                string directDlLink = doc.RootElement.GetProperty("link").GetString()!;

                var dlResp = await _http.GetAsync(directDlLink);
                dlResp.EnsureSuccessStatusCode();
                raw = await dlResp.Content.ReadAsByteArrayAsync();
            }
            else
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, item.DownloadUrl);
                if (item.Source == "SubSource") req.Headers.Add("X-API-Key", SUBSOURCE_KEY);

                var resp = await _http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                raw = await resp.Content.ReadAsByteArrayAsync();
            }

            byte[] srtBytes = ExtractBytes(raw);
            string text = FixEncoding(srtBytes);
            await File.WriteAllTextAsync(targetPath, text, Encoding.UTF8);

            return targetPath;
        }

        private byte[] ExtractBytes(byte[] raw)
        {
            if (raw.Length > 4 && raw[0] == 0x50 && raw[1] == 0x4B)
            {
                using var ms = new MemoryStream(raw);
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".ass", StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    using var s = entry.Open();
                    using var outMs = new MemoryStream();
                    s.CopyTo(outMs);
                    return outMs.ToArray();
                }
            }
            if (raw.Length > 2 && raw[0] == 0x1F && raw[1] == 0x8B)
            {
                using var ms = new MemoryStream(raw);
                using var gz = new GZipStream(ms, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                gz.CopyTo(outMs);
                return outMs.ToArray();
            }
            return raw;
        }

        private string FixEncoding(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

            string utf8 = Encoding.UTF8.GetString(bytes);
            if (!utf8.Contains('\uFFFD') && Regex.IsMatch(utf8, @"[\u0600-\u06FF]")) return utf8;

            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var win1256 = Encoding.GetEncoding(1256);
                string win1256Text = win1256.GetString(bytes);
                if (Regex.IsMatch(win1256Text, @"[\u0600-\u06FF]")) return win1256Text;
            }
            catch { }

            return utf8;
        }

        private class SubResultItem
        {
            public string Title { get; set; } = "";
            public string Language { get; set; } = "";
            public string LanguageCode { get; set; } = "fa";
            public string DownloadUrl { get; set; } = "";
            public string Source { get; set; } = "";
            public int? Season { get; set; }
            public int? Episode { get; set; }
            public string? ReleaseInfo { get; set; }
            public int? OpenSubFileId { get; set; }
        }
    }
}
