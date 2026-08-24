using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MpvMenuHelper
{
    public class SubtitleStyleForm : Form
    {
        private readonly MpvIpcClient _ipc;

        private TrackBar? _tbFontSize;
        private Label? _lblFontSizeVal;

        private TrackBar? _tbBorderSize;
        private Label? _lblBorderSizeVal;

        private TrackBar? _tbMarginY;
        private Label? _lblMarginYVal;

        private TrackBar? _tbOpacity;
        private Label? _lblOpacityVal;

        private CheckBox? _chkBold;
        private CheckBox? _chkBackground;

        private string _selectedTextColor = "#FFFF00";
        private string _selectedBorderColor = "#000000";
        private string _selectedBgColor = "#000000";

        private int _fontSize = 46;
        private int _borderSize = 3;
        private int _marginY = 48;
        private int _opacity = 75;
        private bool _isBold = true;
        private bool _hasBg = false;

        private readonly List<Action> _colorRepainters = new();

        public SubtitleStyleForm(string pipeName)
        {
            _ipc = new MpvIpcClient(pipeName);
            // Enable ASS override so styling applies to all subtitle types (ASS, SSA, SRT, VTT)
            _ipc.SendCommand("set_property", "sub-ass-override", "force");
            _ipc.SendCommand("set", "sub-ass-override", "force");

            LoadSavedSettings();
            InitializeComponent();
        }

        private void LoadSavedSettings()
        {
            try
            {
                string? confPath = GetConfigPath();
                if (confPath != null && File.Exists(confPath))
                {
                    var lines = File.ReadAllLines(confPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                        var parts = trimmed.Split('=', 2);
                        if (parts.Length != 2) continue;

                        string key = parts[0].Trim().ToLowerInvariant();
                        string val = parts[1].Trim().Trim('\"', '\'');

                        if (key == "sub-font-size" && int.TryParse(val, out var fs)) _fontSize = fs;
                        else if (key == "sub-color") _selectedTextColor = val.ToUpperInvariant();
                        else if (key == "sub-border-size" && int.TryParse(val, out var bs)) _borderSize = bs;
                        else if (key == "sub-border-color") _selectedBorderColor = val.ToUpperInvariant();
                        else if (key == "sub-margin-y" && int.TryParse(val, out var my)) _marginY = my;
                        else if (key == "sub-bold") _isBold = val.Equals("yes", StringComparison.OrdinalIgnoreCase);
                        else if (key == "sub-box") _hasBg = val.Equals("yes", StringComparison.OrdinalIgnoreCase);
                        else if (key == "sub-back-color" && val.Length == 9 && val.StartsWith("#"))
                        {
                            // #AARRGGBB
                            if (int.TryParse(val.Substring(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var a))
                            {
                                _opacity = (int)(a / 2.55);
                            }
                            _selectedBgColor = ("#" + val.Substring(3)).ToUpperInvariant();
                        }
                    }
                }
            }
            catch { }
        }

        private string? GetConfigPath()
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop");
            var appDataConf = Path.Combine(appData, "sub_style.conf");
            if (File.Exists(appDataConf)) return appDataConf;

            var localConf = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sub_style.conf");
            if (File.Exists(localConf)) return localConf;

            return appDataConf;
        }

        private void SaveSettings()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Subtitle Customization Config");
                sb.AppendLine("sub-ass-override=force");
                sb.AppendLine($"sub-font-size={_fontSize}");
                double scale = Math.Round((double)_fontSize / 46.0, 2);
                sb.AppendLine($"sub-scale={scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.AppendLine($"sub-color=\"{_selectedTextColor}\"");
                sb.AppendLine($"sub-border-size={_borderSize}");
                sb.AppendLine($"sub-border-color=\"{_selectedBorderColor}\"");
                sb.AppendLine($"sub-margin-y={_marginY}");
                sb.AppendLine($"sub-bold={(_isBold ? "yes" : "no")}");

                if (_hasBg)
                {
                    int alpha = (int)(_opacity * 2.55);
                    string alphaHex = alpha.ToString("X2");
                    string colorRaw = _selectedBgColor.TrimStart('#');
                    sb.AppendLine("sub-box=yes");
                    sb.AppendLine("sub-border-style=background-box");
                    sb.AppendLine($"sub-back-color=\"#{alphaHex}{colorRaw}\"");
                }
                else
                {
                    sb.AppendLine("sub-box=no");
                    sb.AppendLine("sub-border-style=outline-and-shadow");
                    sb.AppendLine("sub-back-color=\"#00000000\"");
                }

                string content = sb.ToString();

                // 1. AppData
                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop");
                Directory.CreateDirectory(appData);
                File.WriteAllText(Path.Combine(appData, "sub_style.conf"), content, Encoding.UTF8);

                // 2. BaseDir
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sub_style.conf"), content, Encoding.UTF8);

                // 3. Project Source Dir if exists
                string srcConf = @"c:\Users\ALI\CascadeProjects\MovieManagerDesktop\MPVPlayer\sub_style.conf";
                try { File.WriteAllText(srcConf, content, Encoding.UTF8); } catch { }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "تنظیمات و استایل زیرنویس";
            this.Size = new Size(390, 525);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(22, 22, 26);
            this.ForeColor = Color.White;
            this.RightToLeft = RightToLeft.Yes;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;

            // Header panel for dragging
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(30, 30, 36)
            };

            var lblTitle = new Label
            {
                Text = "🎨 شخصی‌سازی استایل زیرنویس",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 235, 240),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 0, 0, 0)
            };

            var btnClose = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 180, 190),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(38, 40),
                Dock = DockStyle.Left,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 40, 60);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 20, 40);
            btnClose.Click += (s, e) =>
            {
                SaveSettings();
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

            var pnlScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12)
            };

            int top = 10;

            // --- 1. Font Size ---
            top = AddSliderSection(pnlScroll, top, "اندازه متن زیرنویس:", 20, 100, _fontSize, out _tbFontSize, out _lblFontSizeVal, (val) =>
            {
                _fontSize = val;
                _ipc.SendCommand("set_property", "sub-font-size", val);
                _ipc.SendCommand("set", "sub-font-size", val);
                double scale = Math.Round((double)val / 46.0, 2);
                _ipc.SendCommand("set_property", "sub-scale", scale);
                _ipc.SendCommand("set", "sub-scale", scale);
                SaveSettings();
            });

            // --- 2. Text Color Swatches (Circles) ---
            top = AddColorCirclesSection(pnlScroll, top, "رنگ متن:", new[]
            {
                ("#FFFF00", Color.FromArgb(255, 230, 0)),
                ("#FFFFFF", Color.White),
                ("#00FF66", Color.FromArgb(0, 255, 102)),
                ("#00E5FF", Color.FromArgb(0, 229, 255)),
                ("#FF6699", Color.FromArgb(255, 102, 153)),
                ("#FF3333", Color.FromArgb(255, 51, 51))
            }, () => _selectedTextColor, (hex) =>
            {
                _selectedTextColor = hex.ToUpperInvariant();
                _ipc.SendCommand("set_property", "sub-color", _selectedTextColor);
                _ipc.SendCommand("set", "sub-color", _selectedTextColor);
                SaveSettings();
                RefreshColorChips();
            });

            // --- 3. Border Size ---
            top = AddSliderSection(pnlScroll, top, "ضخامت کادر دور متن:", 0, 8, _borderSize, out _tbBorderSize, out _lblBorderSizeVal, (val) =>
            {
                _borderSize = val;
                _ipc.SendCommand("set_property", "sub-border-size", val);
                _ipc.SendCommand("set", "sub-border-size", val);
                SaveSettings();
            });

            // --- 4. Border Color Swatches (Circles) ---
            top = AddColorCirclesSection(pnlScroll, top, "رنگ کادر دور متن:", new[]
            {
                ("#000000", Color.FromArgb(10, 10, 10)),
                ("#333333", Color.FromArgb(60, 60, 60)),
                ("#002266", Color.FromArgb(0, 34, 102)),
                ("#660000", Color.FromArgb(102, 0, 0))
            }, () => _selectedBorderColor, (hex) =>
            {
                _selectedBorderColor = hex.ToUpperInvariant();
                _ipc.SendCommand("set_property", "sub-border-color", _selectedBorderColor);
                _ipc.SendCommand("set", "sub-border-color", _selectedBorderColor);
                SaveSettings();
                RefreshColorChips();
            });

            // --- 5. Margin Bottom ---
            top = AddSliderSection(pnlScroll, top, "فاصله از پایین تصویر:", 10, 140, _marginY, out _tbMarginY, out _lblMarginYVal, (val) =>
            {
                _marginY = val;
                _ipc.SendCommand("set_property", "sub-margin-y", val);
                _ipc.SendCommand("set", "sub-margin-y", val);
                SaveSettings();
            });

            // --- 6. Background Box Checkbox & Swatches ---
            var pnlBgRow = new Panel { Location = new Point(12, top), Size = new Size(345, 26) };
            _chkBackground = new CheckBox
            {
                Text = "کادر پس‌زمینه (Background Box)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(240, 220, 100),
                Checked = _hasBg,
                AutoSize = true,
                Location = new Point(0, 2)
            };
            _chkBackground.CheckedChanged += (s, e) =>
            {
                _hasBg = _chkBackground.Checked;
                ApplyBackgroundBox();
                SaveSettings();
            };
            pnlBgRow.Controls.Add(_chkBackground);
            pnlScroll.Controls.Add(pnlBgRow);
            top += 30;

            top = AddColorCirclesSection(pnlScroll, top, "رنگ پس‌زمینه:", new[]
            {
                ("#000000", Color.FromArgb(10, 10, 10)),
                ("#262626", Color.FromArgb(40, 40, 40)),
                ("#0A1931", Color.FromArgb(10, 25, 49)),
                ("#4A0E17", Color.FromArgb(74, 14, 23))
            }, () => _selectedBgColor, (hex) =>
            {
                _selectedBgColor = hex.ToUpperInvariant();
                if (_chkBackground != null && !_chkBackground.Checked)
                {
                    _chkBackground.Checked = true;
                }
                else
                {
                    ApplyBackgroundBox();
                }
                SaveSettings();
                RefreshColorChips();
            });

            top = AddSliderSection(pnlScroll, top, "میزان شفافیت پس‌زمینه (%):", 10, 100, _opacity, out _tbOpacity, out _lblOpacityVal, (val) =>
            {
                _opacity = val;
                if (_chkBackground?.Checked == true)
                {
                    ApplyBackgroundBox();
                }
                SaveSettings();
            });

            // --- 7. Bold & Reset Buttons ---
            var pnlBottomActions = new Panel { Location = new Point(12, top), Size = new Size(345, 40) };

            _chkBold = new CheckBox
            {
                Text = "متن ضخیم (Bold)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                Checked = _isBold,
                AutoSize = true,
                Location = new Point(0, 8)
            };
            _chkBold.CheckedChanged += (s, e) =>
            {
                _isBold = _chkBold.Checked;
                string boldVal = _isBold ? "yes" : "no";
                _ipc.SendCommand("set_property", "sub-bold", boldVal);
                _ipc.SendCommand("set", "sub-bold", boldVal);
                SaveSettings();
            };

            var btnReset = new Button
            {
                Text = "بازنشانی",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 60),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(95, 30),
                Location = new Point(245, 4),
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 50, 65);
            btnReset.Click += (s, e) => ResetDefaults();

            pnlBottomActions.Controls.Add(_chkBold);
            pnlBottomActions.Controls.Add(btnReset);
            pnlScroll.Controls.Add(pnlBottomActions);
            top += 46;

            this.Controls.Add(pnlScroll);

            // Border painting
            this.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(220, 50, 65), 1.2f);
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            };
        }

        private void RefreshColorChips()
        {
            foreach (var repaint in _colorRepainters)
            {
                repaint();
            }
        }

        private void ApplyBackgroundBox()
        {
            if (_chkBackground?.Checked == true)
            {
                int opacity = _tbOpacity?.Value ?? 75;
                int alpha = (int)(opacity * 2.55);
                string alphaHex = alpha.ToString("X2");
                string colorRaw = _selectedBgColor.TrimStart('#');

                _ipc.SendCommand("set_property", "sub-ass-override", "force");
                _ipc.SendCommand("set_property", "sub-box", "yes");
                _ipc.SendCommand("set", "sub-box", "yes");
                _ipc.SendCommand("set_property", "sub-border-style", "background-box");
                _ipc.SendCommand("set", "sub-border-style", "background-box");
                _ipc.SendCommand("set_property", "sub-back-color", $"#{alphaHex}{colorRaw}");
                _ipc.SendCommand("set", "sub-back-color", $"#{alphaHex}{colorRaw}");
            }
            else
            {
                _ipc.SendCommand("set_property", "sub-box", "no");
                _ipc.SendCommand("set", "sub-box", "no");
                _ipc.SendCommand("set_property", "sub-border-style", "outline-and-shadow");
                _ipc.SendCommand("set", "sub-border-style", "outline-and-shadow");
                _ipc.SendCommand("set_property", "sub-back-color", "#00000000");
                _ipc.SendCommand("set", "sub-back-color", "#00000000");
            }
        }

        private int AddSliderSection(Panel container, int top, string title, int min, int max, int defaultVal, out TrackBar trackBar, out Label lblVal, Action<int> onValChanged)
        {
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 215),
                Location = new Point(12, top),
                AutoSize = true
            };
            container.Controls.Add(lblTitle);
            top += 22;

            var pnlSlider = new Panel { Location = new Point(12, top), Size = new Size(345, 30) };

            var tb = new TrackBar
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, defaultVal)),
                TickStyle = TickStyle.None,
                Size = new Size(285, 28),
                Location = new Point(0, 0),
                Cursor = Cursors.Hand
            };

            var lbl = new Label
            {
                Text = tb.Value.ToString(),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 215, 0),
                Size = new Size(50, 24),
                Location = new Point(290, 2),
                TextAlign = ContentAlignment.MiddleCenter
            };

            tb.ValueChanged += (s, e) =>
            {
                lbl.Text = tb.Value.ToString();
                onValChanged(tb.Value);
            };

            pnlSlider.Controls.Add(tb);
            pnlSlider.Controls.Add(lbl);
            container.Controls.Add(pnlSlider);

            trackBar = tb;
            lblVal = lbl;
            return top + 34;
        }

        private int AddColorCirclesSection(Panel container, int top, string title, (string hex, Color color)[] palette, Func<string> getSelectedHex, Action<string> onColorSelected)
        {
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 215),
                Location = new Point(12, top),
                AutoSize = true
            };
            container.Controls.Add(lblTitle);
            top += 22;

            var pnlColors = new Panel { Location = new Point(12, top), Size = new Size(345, 32) };
            int left = 0;

            foreach (var item in palette)
            {
                string hex = item.hex.ToUpperInvariant();
                Color color = item.color;

                var chip = new Panel
                {
                    Size = new Size(26, 26),
                    Location = new Point(left, 2),
                    Cursor = Cursors.Hand
                };

                chip.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    bool isSelected = getSelectedHex().Equals(hex, StringComparison.OrdinalIgnoreCase);

                    using var brush = new SolidBrush(color);
                    e.Graphics.FillEllipse(brush, 2, 2, 21, 21);

                    if (isSelected)
                    {
                        using var penSelect = new Pen(Color.FromArgb(220, 50, 65), 2.5f);
                        e.Graphics.DrawEllipse(penSelect, 1, 1, 23, 23);
                    }
                    else
                    {
                        using var penNormal = new Pen(Color.FromArgb(100, 100, 110), 1f);
                        e.Graphics.DrawEllipse(penNormal, 2, 2, 21, 21);
                    }
                };

                chip.Click += (s, e) => onColorSelected(hex);
                _colorRepainters.Add(() => chip.Invalidate());

                pnlColors.Controls.Add(chip);
                left += 34;
            }

            container.Controls.Add(pnlColors);
            return top + 36;
        }

        private void ResetDefaults()
        {
            _fontSize = 46;
            _borderSize = 3;
            _marginY = 48;
            _opacity = 75;
            _isBold = true;
            _hasBg = false;
            _selectedTextColor = "#FFFFFF";
            _selectedBorderColor = "#000000";
            _selectedBgColor = "#000000";

            if (_tbFontSize != null) _tbFontSize.Value = 46;
            if (_tbBorderSize != null) _tbBorderSize.Value = 3;
            if (_tbMarginY != null) _tbMarginY.Value = 48;
            if (_tbOpacity != null) _tbOpacity.Value = 75;
            if (_chkBold != null) _chkBold.Checked = true;
            if (_chkBackground != null) _chkBackground.Checked = false;

            _ipc.SendCommand("set_property", "sub-font-size", 46);
            _ipc.SendCommand("set", "sub-font-size", 46);
            _ipc.SendCommand("set_property", "sub-scale", 1.0);
            _ipc.SendCommand("set", "sub-scale", 1.0);
            _ipc.SendCommand("set_property", "sub-color", "#FFFFFF");
            _ipc.SendCommand("set", "sub-color", "#FFFFFF");
            _ipc.SendCommand("set_property", "sub-border-size", 3);
            _ipc.SendCommand("set", "sub-border-size", 3);
            _ipc.SendCommand("set_property", "sub-border-color", "#000000");
            _ipc.SendCommand("set", "sub-border-color", "#000000");
            _ipc.SendCommand("set_property", "sub-margin-y", 48);
            _ipc.SendCommand("set", "sub-margin-y", 48);
            _ipc.SendCommand("set_property", "sub-bold", "yes");
            _ipc.SendCommand("set", "sub-bold", "yes");
            _ipc.SendCommand("set_property", "sub-box", "no");
            _ipc.SendCommand("set", "sub-box", "no");
            _ipc.SendCommand("set_property", "sub-border-style", "outline-and-shadow");
            _ipc.SendCommand("set", "sub-border-style", "outline-and-shadow");
            _ipc.SendCommand("set_property", "sub-back-color", "#00000000");
            _ipc.SendCommand("set", "sub-back-color", "#00000000");

            SaveSettings();
            RefreshColorChips();
        }
    }
}
