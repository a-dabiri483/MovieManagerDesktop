using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MpvMenuHelper
{
    public class SubtitleDownloadForm : Form
    {
        private readonly string _pipeName;
        private readonly string _videoPath;
        private readonly MpvIpcClient _ipc;

        private TextBox txtSearch;
        private ComboBox cmbLanguage;
        private Button btnSearch;
        private ListView lvSubtitles;
        private Button btnDownload;
        private Button btnCancel;
        private Label lblStatus;
        private ProgressBar progressBar;

        private readonly List<SubResultItem> _searchResults = new();

        public SubtitleDownloadForm(string pipeName, string videoPath)
        {
            _pipeName = pipeName;
            _videoPath = videoPath;
            _ipc = new MpvIpcClient(pipeName);

            InitializeComponent();
            ApplyDarkTheme();

            // Auto populate search box from video filename
            string initialQuery = ExtractQueryFromPath(videoPath);
            txtSearch.Text = initialQuery;

            this.Load += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(initialQuery))
                {
                    await PerformSearchAsync();
                }
            };
        }

        private string ExtractQueryFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            string name = Path.GetFileNameWithoutExtension(path);
            name = Regex.Replace(name, @"[\.\[\]\(\)\-_]", " ");
            name = Regex.Replace(name, @"\b(?:1080p|720p|480p|2160p|4k|bluray|web-dl|webrip|x264|x265|hevc|yify|pahe|psa|rarbg|eztv|galaxytv)\b.*", "", RegexOptions.IgnoreCase);
            return Regex.Replace(name, @"\s+", " ").Trim();
        }

        private void InitializeComponent()
        {
            this.Text = "دانلود آنلاین زیرنویس (SubDL & SubSource)";
            this.Size = new Size(720, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(16)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Search bar
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // List
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Progress/Status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Buttons

            // 1. Search Bar Panel
            var pnlSearch = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5f)
            };
            txtSearch.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await PerformSearchAsync();
                }
            };

            cmbLanguage = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f)
            };
            cmbLanguage.Items.AddRange(new object[] { "🇮🇷 همه زبان‌ها", "🇮🇷 فقط فارسی", "🇬🇧 فقط انگلیسی" });
            cmbLanguage.SelectedIndex = 0;

            btnSearch = new Button
            {
                Text = "🔍 جستجو",
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnSearch.Click += async (s, e) => await PerformSearchAsync();

            pnlSearch.Controls.Add(txtSearch, 0, 0);
            pnlSearch.Controls.Add(cmbLanguage, 1, 0);
            pnlSearch.Controls.Add(btnSearch, 2, 0);

            // 2. ListView
            lvSubtitles = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9.5f)
            };
            lvSubtitles.Columns.Add("زبان", 100, HorizontalAlignment.Right);
            lvSubtitles.Columns.Add("عنوان و نسخه انتشار", 460, HorizontalAlignment.Right);
            lvSubtitles.Columns.Add("منبع", 90, HorizontalAlignment.Center);
            lvSubtitles.DoubleClick += async (s, e) => await PerformDownloadAsync();

            // 3. Status
            lblStatus = new Label
            {
                Text = "برای جستجوی زیرنویس، عنوان را وارد کرده و دکمه جستجو را بزنید.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.LightGray
            };

            // 4. Buttons Panel
            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            btnDownload = new Button
            {
                Text = "⬇️ دانلود و اعمال روی فیلم",
                Size = new Size(180, 38),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Enabled = false
            };
            btnDownload.Click += async (s, e) => await PerformDownloadAsync();

            btnCancel = new Button
            {
                Text = "بستن",
                Size = new Size(90, 38),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => this.Close();

            pnlButtons.Controls.Add(btnDownload);
            pnlButtons.Controls.Add(btnCancel);

            lvSubtitles.SelectedIndexChanged += (s, e) =>
            {
                btnDownload.Enabled = lvSubtitles.SelectedItems.Count > 0;
            };

            mainLayout.Controls.Add(pnlSearch, 0, 0);
            mainLayout.Controls.Add(lvSubtitles, 0, 1);
            mainLayout.Controls.Add(lblStatus, 0, 2);
            mainLayout.Controls.Add(pnlButtons, 0, 3);

            this.Controls.Add(mainLayout);
        }

        private void ApplyDarkTheme()
        {
            this.BackColor = Color.FromArgb(24, 24, 36);
            this.ForeColor = Color.White;

            txtSearch.BackColor = Color.FromArgb(34, 34, 52);
            txtSearch.ForeColor = Color.White;

            cmbLanguage.BackColor = Color.FromArgb(34, 34, 52);
            cmbLanguage.ForeColor = Color.White;

            btnSearch.BackColor = Color.FromArgb(58, 134, 255);
            btnSearch.ForeColor = Color.White;
            btnSearch.FlatStyle = FlatStyle.Flat;
            btnSearch.FlatAppearance.BorderSize = 0;

            lvSubtitles.BackColor = Color.FromArgb(18, 18, 28);
            lvSubtitles.ForeColor = Color.White;

            btnDownload.BackColor = Color.FromArgb(46, 213, 115);
            btnDownload.ForeColor = Color.Black;
            btnDownload.FlatStyle = FlatStyle.Flat;
            btnDownload.FlatAppearance.BorderSize = 0;

            btnCancel.BackColor = Color.FromArgb(40, 40, 60);
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
        }

        private async Task PerformSearchAsync()
        {
            string query = txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            btnSearch.Enabled = false;
            btnDownload.Enabled = false;
            lblStatus.Text = "در حال جستجو در منابع زیرنویس...";
            lblStatus.ForeColor = Color.FromArgb(0, 210, 211);
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
                    lvi.SubItems.Add(item.Title);
                    lvi.SubItems.Add(item.Source);
                    lvi.Tag = item;
                    lvSubtitles.Items.Add(lvi);
                }

                if (_searchResults.Count > 0)
                {
                    lblStatus.Text = $"{_searchResults.Count} زیرنویس پیدا شد. زیرنویس مورد نظر را انتخاب و دکمه دانلود را بزنید.";
                    lblStatus.ForeColor = Color.FromArgb(46, 213, 115);
                    lvSubtitles.Items[0].Selected = true;
                }
                else
                {
                    lblStatus.Text = "زیرنویسی برای این عنوان یافت نشد. لطفاً نام لاتین یا مشخصات دیگر را جستجو کنید.";
                    lblStatus.ForeColor = Color.FromArgb(255, 71, 87);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"خطا در جستجو: {ex.Message}";
                lblStatus.ForeColor = Color.FromArgb(255, 71, 87);
            }
            finally
            {
                btnSearch.Enabled = true;
            }
        }

        private async Task PerformDownloadAsync()
        {
            if (lvSubtitles.SelectedItems.Count == 0) return;
            var item = (SubResultItem)lvSubtitles.SelectedItems[0].Tag;

            btnDownload.Enabled = false;
            btnSearch.Enabled = false;
            lblStatus.Text = "در حال دانلود، تبدیل انکودینگ و الصاق به فیلم...";
            lblStatus.ForeColor = Color.FromArgb(0, 210, 211);

            try
            {
                string savedSrt = await DownloadAndSaveSubtitleInternalAsync(item, _videoPath);
                
                // Attach to MPV
                string escapedPath = savedSrt.Replace("\\", "/").Replace("\"", "\\\"");
                _ipc.SendCommand($"sub-add \"{escapedPath}\" \"select\"");
                _ipc.SendCommand("set sub-visibility yes");
                _ipc.SendCommand($"show-text \"زیرنویس فعال شد: {Path.GetFileName(savedSrt)}\" 3000");

                lblStatus.Text = $"✅ زیرنویس با موفقیت ذخیره و روی فیلم اعمال شد: {Path.GetFileName(savedSrt)}";
                lblStatus.ForeColor = Color.FromArgb(46, 213, 115);

                await Task.Delay(1200);
                this.Close();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"خطا در دانلود زیرنویس: {ex.Message}";
                lblStatus.ForeColor = Color.FromArgb(255, 71, 87);
                btnDownload.Enabled = true;
                btnSearch.Enabled = true;
            }
        }

        // --- Network Helpers ---
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private const string SUBDL_KEY = "subdl_HHtBliLNdNumqWs29n7Z4E9GLQwyX0bL9MDFc6RTy34";
        private const string SUBSOURCE_KEY = "sk_68d68b32ef82a0a168e243815c66d85ca5ecfe2909507245e8ff695b27c10025";

        private async Task<List<SubResultItem>> SearchOnlineSubtitlesInternalAsync(string rawQuery, string langFilter)
        {
            var results = new List<SubResultItem>();
            string subdlLangs = langFilter == "EN" ? "EN" : (langFilter == "FA" ? "FA" : "FA,EN");

            // SubDL
            try
            {
                string url = $"https://api.subdl.com/api/v1/subtitles?film_name={Uri.EscapeDataString(rawQuery)}&languages={subdlLangs}&api_key={SUBDL_KEY}&subs_per_page=30";
                var resp = await _http.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("status", out var st) && st.GetBoolean() &&
                        root.TryGetProperty("subtitles", out var subsArr) && subsArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var sub in subsArr.EnumerateArray())
                        {
                            string rel = sub.TryGetProperty("release_name", out var rn) ? rn.GetString() ?? "" : "";
                            string lang = sub.TryGetProperty("lang", out var l) ? l.GetString() ?? "FA" : "FA";
                            string u = sub.TryGetProperty("url", out var up) ? up.GetString() ?? "" : "";
                            if (string.IsNullOrEmpty(u)) continue;

                            string fullUrl = u.StartsWith("http") ? u : $"https://dl.subdl.com{u}";
                            string langLabel = lang.Equals("FA", StringComparison.OrdinalIgnoreCase) ? "🇮🇷 فارسی" : "🇬🇧 English";

                            results.Add(new SubResultItem
                            {
                                Title = string.IsNullOrWhiteSpace(rel) ? rawQuery : rel,
                                Language = langLabel,
                                LanguageCode = lang.ToLowerInvariant(),
                                DownloadUrl = fullUrl,
                                Source = "SubDL"
                            });
                        }
                    }
                }
            }
            catch { }

            // SubSource fallback
            if (results.Count < 5)
            {
                try
                {
                    string ssLang = langFilter == "EN" ? "english" : "persian";
                    string sUrl = $"https://api.subsource.net/api/v1/movies/search?searchType=text&q={Uri.EscapeDataString(rawQuery)}";
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

                                            results.Add(new SubResultItem
                                            {
                                                Title = rel,
                                                Language = langLabel,
                                                LanguageCode = ssLang == "persian" ? "fa" : "en",
                                                DownloadUrl = dl,
                                                Source = "SubSource"
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return results.OrderByDescending(r => r.Language.Contains("فارسی")).ToList();
        }

        private async Task<string> DownloadAndSaveSubtitleInternalAsync(SubResultItem item, string targetVideoPath)
        {
            string dir = Path.GetDirectoryName(targetVideoPath)!;
            string baseName = Path.GetFileNameWithoutExtension(targetVideoPath);
            string targetPath = Path.Combine(dir, $"{baseName}.{(item.LanguageCode.Contains("fa") ? "fa" : item.LanguageCode)}.srt");

            using var req = new HttpRequestMessage(HttpMethod.Get, item.DownloadUrl);
            if (item.Source == "SubSource") req.Headers.Add("X-API-Key", SUBSOURCE_KEY);

            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            byte[] raw = await resp.Content.ReadAsByteArrayAsync();
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
        }
    }
}
