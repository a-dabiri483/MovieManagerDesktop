using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;

namespace MpvMenuHelper
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length == 0) return;

            string command = args[0].ToLowerInvariant();

            if (command == "picksub")
            {
                string pipeName = args.Length > 1 ? args[1] : "mpvsocket";
                string initialDir = args.Length > 2 ? args[2] : "";

                using var ofd = new OpenFileDialog
                {
                    Title = "انتخاب فایل زیرنویس",
                    Filter = "فایل‌های زیرنویس (*.srt;*.vtt;*.ass;*.ssa;*.sub;*.txt)|*.srt;*.vtt;*.ass;*.ssa;*.sub;*.txt|همه فایل‌ها (*.*)|*.*",
                    Multiselect = false,
                    CheckFileExists = true,
                    RestoreDirectory = true
                };

                if (!string.IsNullOrEmpty(initialDir))
                {
                    if (Directory.Exists(initialDir))
                    {
                        ofd.InitialDirectory = initialDir;
                    }
                    else if (File.Exists(initialDir))
                    {
                        ofd.InitialDirectory = Path.GetDirectoryName(initialDir);
                    }
                }

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Console.WriteLine(ofd.FileName);
                    Console.Out.Flush();
                }
            }
            else if (command == "sync")
            {
                string pipeName = args.Length > 1 ? args[1] : "mpvsocket";
                double subDelay = args.Length > 2 && double.TryParse(args[2], out var sd) ? sd : 0.0;
                double audioDelay = args.Length > 3 && double.TryParse(args[3], out var ad) ? ad : 0.0;

                Application.Run(new SyncForm(pipeName, subDelay, audioDelay));
            }
            else if (command == "style")
            {
                string pipeName = args.Length > 1 ? args[1] : "mpvsocket";
                Application.Run(new SubtitleStyleForm(pipeName));
            }
            else if (command == "translate")
            {
                string pipeName = args.Length > 1 ? args[1] : "mpvsocket";
                string subPath = args.Length > 2 ? args[2] : "";
                string videoPath = args.Length > 3 ? args[3] : "";
                string sid = args.Length > 4 ? args[4] : "";
                Application.Run(new SubtitleTranslateForm(pipeName, subPath, videoPath, sid));
            }
            else if (command == "downloadsub")
            {
                string pipeName = args.Length > 1 ? args[1] : "mpvsocket";
                string videoPath = args.Length > 2 ? args[2] : "";
                Application.Run(new SubtitleDownloadForm(pipeName, videoPath));
            }
            else
            {
                // Default: Context Menu
                string jsonPath = (command == "menu" && args.Length > 1) ? args[1] : args[0];
                ShowContextMenu(jsonPath);
            }
        }

        private static void ShowContextMenu(string jsonPath)
        {
            // Kill any older menu helpers showing context menu
            var currentProcess = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(currentProcess.ProcessName))
            {
                if (process.Id != currentProcess.Id)
                {
                    try { process.Kill(); } catch { }
                }
            }

            try
            {
                if (!File.Exists(jsonPath)) return;

                string json = File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(json);

                var menu = new ContextMenuStrip();
                menu.RightToLeft = RightToLeft.Yes;
                menu.Renderer = new DarkRenderer();
                menu.ForeColor = Color.White;
                menu.Font = new Font("Segoe UI", 10F);

                BuildMenu(menu.Items, doc.RootElement.GetProperty("items"));

                var dummyForm = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = Cursor.Position,
                    Size = new Size(1, 1),
                    TopMost = true,
                    Opacity = 0.01
                };

                dummyForm.Shown += (s, e) =>
                {
                    dummyForm.Activate();
                    menu.Show(dummyForm, new Point(0, 0));
                };

                menu.Closed += (s, e) =>
                {
                    dummyForm.Close();
                    Application.Exit();
                };

                Application.Run(dummyForm);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERR:" + ex.Message);
            }
        }

        static void BuildMenu(ToolStripItemCollection collection, JsonElement itemsArray)
        {
            foreach (var item in itemsArray.EnumerateArray())
            {
                string type = item.GetProperty("type").GetString() ?? "";
                if (type == "separator")
                {
                    collection.Add(new ToolStripSeparator());
                }
                else if (type == "item")
                {
                    string text = item.GetProperty("text").GetString() ?? "";
                    string id = item.GetProperty("id").GetString() ?? "";
                    bool isChecked = false;
                    if (item.TryGetProperty("checked", out var checkedProp))
                        isChecked = checkedProp.GetBoolean();

                    var menuItem = new ToolStripMenuItem(text);
                    menuItem.ForeColor = Color.White;
                    menuItem.Checked = isChecked;
                    menuItem.Click += (s, e) =>
                    {
                        Console.WriteLine(id);
                        Application.Exit();
                    };
                    collection.Add(menuItem);
                }
                else if (type == "submenu")
                {
                    string text = item.GetProperty("text").GetString() ?? "";
                    var submenu = new ToolStripMenuItem(text);
                    submenu.ForeColor = Color.White;
                    submenu.DropDown.RightToLeft = RightToLeft.Yes;
                    submenu.DropDown.Renderer = new DarkRenderer();
                    BuildMenu(submenu.DropDownItems, item.GetProperty("items"));
                    collection.Add(submenu);
                }
            }
        }
    }

    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { }
    }

    class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(64, 64, 64);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(64, 64, 64);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(64, 64, 64);
        public override Color MenuBorder => Color.FromArgb(28, 28, 28);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color ToolStripDropDownBackground => Color.FromArgb(32, 32, 32);
        public override Color ImageMarginGradientBegin => Color.FromArgb(32, 32, 32);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(32, 32, 32);
        public override Color ImageMarginGradientEnd => Color.FromArgb(32, 32, 32);
        public override Color SeparatorDark => Color.FromArgb(80, 80, 80);
        public override Color SeparatorLight => Color.Transparent;
    }
}
