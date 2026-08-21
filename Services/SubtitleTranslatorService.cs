using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class SubtitleCue
    {
        public string Index { get; set; } = string.Empty;
        public string Timecode { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public long StartMs { get; set; }
        public long EndMs { get; set; }
    }

    public class SubtitleTranslationProgressInfo
    {
        public int CurrentBatch { get; set; }
        public int TotalBatches { get; set; }
        public int TranslatedLines { get; set; }
        public int TotalLines { get; set; }
        public double Percent { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    public static class SubtitleTranslatorService
    {
        private static readonly HttpClient _httpClient = new HttpClient(new Network.ProxyHttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public static List<SubtitleCue> ParseSubtitleFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return new List<SubtitleCue>();

            try
            {
                string rawText = ReadFileWithEncodingFallback(filePath);
                var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                return ParseSrtCues(lines);
            }
            catch (Exception ex)
            {
                LoggerService.Error("Failed to parse subtitle file", ex);
                return new List<SubtitleCue>();
            }
        }

        /// <summary>
        /// Translates an SRT or VTT subtitle file to Persian asynchronously in batches.
        /// </summary>
        public static async Task<(bool success, string? outputPath, string message)> TranslateSubtitleFileAsync(
            string subtitleFilePath, 
            string targetLang = "fa", 
            IProgress<SubtitleTranslationProgressInfo>? progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(subtitleFilePath) || !File.Exists(subtitleFilePath))
            {
                return (false, null, "فایل زیرنویس یافت نشد.");
            }

            try
            {
                // 1. Read with auto-encoding detection (Windows-1256 vs UTF-8)
                string rawText = ReadFileWithEncodingFallback(subtitleFilePath);
                var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var cues = ParseSrtCues(lines);

                if (cues.Count == 0)
                {
                    return (false, null, "فرمت فایل زیرنویس معتبر نیست یا متنی در آن یافت نشد.");
                }

                int batchSize = 35;
                int totalBatches = (int)Math.Ceiling((double)cues.Count / batchSize);
                var translatedCues = new List<SubtitleCue>();
                int consecutiveFailures = 0;

                for (int i = 0; i < cues.Count; i += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = cues.Skip(i).Take(batchSize).ToList();
                    var sb = new StringBuilder();

                    for (int idx = 0; idx < batch.Count; idx++)
                    {
                        string cleanText = batch[idx].Text.Replace("\r", " ").Replace("\n", " ");
                        sb.AppendLine($"{idx + 1}. {cleanText}");
                    }

                    string? translatedBatch = await TranslateTextBatchAsync(sb.ToString().Trim(), targetLang, cancellationToken);
                    if (string.IsNullOrWhiteSpace(translatedBatch))
                    {
                        consecutiveFailures++;
                        if (consecutiveFailures >= 3)
                        {
                            return (false, null, "خطا در اتصال به سرور ترجمه آنلاین. لطفاً اتصال اینترنت خود را بررسی کنید.");
                        }
                    }
                    else
                    {
                        consecutiveFailures = 0;
                    }

                    var translatedMap = !string.IsNullOrWhiteSpace(translatedBatch) 
                        ? ParseNumberedLines(translatedBatch) 
                        : new Dictionary<int, string>();

                    for (int idx = 0; idx < batch.Count; idx++)
                    {
                        int oneBased = idx + 1;
                        string transText = translatedMap.ContainsKey(oneBased) && !string.IsNullOrWhiteSpace(translatedMap[oneBased])
                            ? translatedMap[oneBased]
                            : batch[idx].Text;

                        translatedCues.Add(new SubtitleCue
                        {
                            Index = batch[idx].Index,
                            Timecode = batch[idx].Timecode,
                            Text = transText
                        });
                    }

                    int currentBatch = (i / batchSize) + 1;
                    int processedLines = Math.Min(i + batch.Count, cues.Count);
                    double currentProgress = Math.Round(((double)processedLines / cues.Count) * 100.0, 1);

                    progress?.Report(new SubtitleTranslationProgressInfo
                    {
                        CurrentBatch = currentBatch,
                        TotalBatches = totalBatches,
                        TranslatedLines = processedLines,
                        TotalLines = cues.Count,
                        Percent = currentProgress,
                        StatusText = $"پارت {currentBatch}/{totalBatches} ({processedLines}/{cues.Count})"
                    });
                }

                // 2. Save translated file next to the video/original subtitle
                string dir = Path.GetDirectoryName(subtitleFilePath) ?? string.Empty;
                string baseName = Path.GetFileNameWithoutExtension(subtitleFilePath);
                
                if (baseName.EndsWith(".fa", StringComparison.OrdinalIgnoreCase) || baseName.EndsWith("_fa", StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName.Substring(0, baseName.Length - 3);
                }

                string outputPath = Path.Combine(dir, $"{baseName}.fa.srt");

                var srtOutput = new StringBuilder();
                for (int i = 0; i < translatedCues.Count; i++)
                {
                    var cue = translatedCues[i];
                    srtOutput.AppendLine((i + 1).ToString());
                    srtOutput.AppendLine(cue.Timecode.Replace(".", ","));
                    srtOutput.AppendLine(cue.Text);
                    srtOutput.AppendLine();
                }

                await File.WriteAllTextAsync(outputPath, srtOutput.ToString(), Encoding.UTF8);
                return (true, outputPath, $"زیرنویس با موفقیت به فارسی ترجمه شد ({translatedCues.Count} خط).");
            }
            catch (OperationCanceledException)
            {
                return (false, null, "ترجمه زیرنویس توسط کاربر متوقف شد.");
            }
            catch (Exception ex)
            {
                LoggerService.Error("Subtitle translation error", ex);
                return (false, null, $"خطا در ترجمه زیرنویس: {ex.Message}");
            }
        }

        public static readonly char[] WIN1256_PERSIAN_MAP = new char[]
        {
            '\u20AC', // 0x80 - Euro
            '\u067E', // 0x81 - پ (Peh)
            '\u201A', // 0x82 - Single low-9 quotation mark
            '\u0192', // 0x83 - Latin small letter f with hook
            '\u201E', // 0x84 - Double low-9 quotation mark
            '\u2026', // 0x85 - Horizontal ellipsis (…)
            '\u2020', // 0x86 - Dagger
            '\u2021', // 0x87 - Double dagger
            '\u02C6', // 0x88 - Modifier letter circumflex accent
            '\u2030', // 0x89 - Per mille sign
            '\u0679', // 0x8A - Tteh (Urdu)
            '\u2039', // 0x8B - Single left-pointing angle quotation mark
            '\u0152', // 0x8C - Latin capital ligature OE
            '\u0686', // 0x8D - چ (Tcheh)
            '\u0698', // 0x8E - ژ (Zheh)
            '\u0688', // 0x8F - Ddal (Urdu)
            '\u06AF', // 0x90 - گ (Gaf)
            '\u2018', // 0x91 - Left single quotation mark
            '\u2019', // 0x92 - Right single quotation mark
            '\u201C', // 0x93 - Left double quotation mark
            '\u201D', // 0x94 - Right double quotation mark
            '\u2022', // 0x95 - Bullet
            '\u2013', // 0x96 - En dash
            '\u2014', // 0x97 - Em dash
            '\u06A9', // 0x98 - ک (Keheh)
            '\u2122', // 0x99 - Trade mark sign
            '\u0691', // 0x9A - Rreh (Urdu)
            '\u203A', // 0x9B - Single right-pointing angle quotation mark
            '\u0153', // 0x9C - Latin small ligature oe
            '\u200C', // 0x9D - ZWNJ (نیم‌فاصله)
            '\u200D', // 0x9E - ZWJ
            '\u06BA', // 0x9F - Noon Ghunna
            '\u00A0', // 0xA0 - Non-breaking space
            '\u060C', // 0xA1 - Arabic comma (،)
            '\u00A2', // 0xA2 - Cent sign
            '\u00A3', // 0xA3 - Pound sign
            '\u00A4', // 0xA4 - Currency sign
            '\u00A5', // 0xA5 - Yen sign
            '\u00A6', // 0xA6 - Broken bar
            '\u00A7', // 0xA7 - Section sign
            '\u00A8', // 0xA8 - Diaeresis
            '\u00A9', // 0xA9 - Copyright sign
            '\u06CC', // 0xAA - ی (Farsi Yeh)
            '\u00AB', // 0xAB - Left-pointing double angle quotation mark («)
            '\u00AC', // 0xAC - Not sign
            '\u00AD', // 0xAD - Soft hyphen
            '\u00AE', // 0xAE - Registered sign
            '\u00AF', // 0xAF - Macron
            '\u00B0', // 0xB0 - Degree sign
            '\u00B1', // 0xB1 - Plus-minus sign
            '\u00B2', // 0xB2 - Superscript two
            '\u00B3', // 0xB3 - Superscript three
            '\u00B4', // 0xB4 - Acute accent
            '\u00B5', // 0xB5 - Micro sign
            '\u00B6', // 0xB6 - Pilcrow sign
            '\u00B7', // 0xB7 - Middle dot
            '\u00B8', // 0xB8 - Cedilla
            '\u00B9', // 0xB9 - Superscript one
            '\u061B', // 0xBA - Arabic semicolon (؛)
            '\u00BB', // 0xBB - Right-pointing double angle quotation mark (»)
            '\u00BC', // 0xBC - Fraction 1/4
            '\u00BD', // 0xBD - Fraction 1/2
            '\u00BE', // 0xBE - Fraction 3/4
            '\u061F', // 0xBF - Arabic question mark (؟)
            '\u06C1', // 0xC0 - Heh Goal
            '\u0621', // 0xC1 - Hamza (ء)
            '\u0622', // 0xC2 - Alef with Madda (آ)
            '\u0623', // 0xC3 - Alef with Hamza Above (أ)
            '\u0624', // 0xC4 - Waw with Hamza Above (ؤ)
            '\u0625', // 0xC5 - Alef with Hamza Below (إ)
            '\u0626', // 0xC6 - Yeh with Hamza Above (ئ)
            '\u0627', // 0xC7 - Alef (ا)
            '\u0628', // 0xC8 - Beh (ب)
            '\u0629', // 0xC9 - Teh Marbuta (ة)
            '\u062A', // 0xCA - Teh (ت)
            '\u062B', // 0xCB - Theh (ث)
            '\u062C', // 0xCC - Jeem (ج)
            '\u062D', // 0xCD - Hah (ح)
            '\u062E', // 0xCE - Khah (خ)
            '\u062F', // 0xCF - Dal (د)
            '\u0630', // 0xD0 - Thal (ذ)
            '\u0631', // 0xD1 - Reh (ر)
            '\u0632', // 0xD2 - Zain (ز)
            '\u0633', // 0xD3 - Seen (س)
            '\u0634', // 0xD4 - Sheen (ش)
            '\u0635', // 0xD5 - Sad (ص)
            '\u0636', // 0xD6 - Dad (ض)
            '\u00D7', // 0xD7 - Multiplication sign (×)
            '\u0637', // 0xD8 - Tah (ط)
            '\u0638', // 0xD9 - Zah (ظ)
            '\u0639', // 0xDA - Ain (ع)
            '\u063A', // 0xDB - Ghain (غ)
            '\u0640', // 0xDC - Tatweel (ـ)
            '\u0641', // 0xDD - Feh (ف)
            '\u0642', // 0xDE - Qaf (ق)
            '\u0643', // 0xDF - Kaf (ك)
            '\u00E0', // 0xE0 - à
            '\u0644', // 0xE1 - Lam (ل)
            '\u00E2', // 0xE2 - â
            '\u0645', // 0xE3 - Meem (م)
            '\u0646', // 0xE4 - Noon (ن)
            '\u0647', // 0xE5 - Heh (ه)
            '\u0648', // 0xE6 - Waw (و)
            '\u00E7', // 0xE7 - ç
            '\u00E8', // 0xE8 - è
            '\u00E9', // 0xE9 - é
            '\u00EA', // 0xEA - ê
            '\u00EB', // 0xEB - ë
            '\u0649', // 0xEC - Alef Maksura (ى)
            '\u064A', // 0xED - Yeh (ي)
            '\u00EE', // 0xEE - î
            '\u00EF', // 0xEF - ï
            '\u064B', // 0xF0 - Fathatan (ً)
            '\u064C', // 0xF1 - Dammatan (ٌ)
            '\u064D', // 0xF2 - Kasratan (ٍ)
            '\u064E', // 0xF3 - Fatha (َ)
            '\u064F', // 0xF4 - Damma (ُ)
            '\u0650', // 0xF5 - Kasra (ِ)
            '\u0651', // 0xF6 - Shadda (ّ)
            '\u0652', // 0xF7 - Sukun (ْ)
            '\u00F7', // 0xF8 - Division sign (÷)
            '\u00F9', // 0xF9 - ù
            '\u0653', // 0xFA - Maddah Above
            '\u00FB', // 0xFB - û
            '\u00FC', // 0xFC - ü
            '\u200E', // 0xFD - LRM
            '\u200F', // 0xFE - RLM
            '\u06D2'  // 0xFF - Yeh Barree
        };

        public static string FixSubtitleEncoding(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return filePath;
                string text = ReadFileWithEncodingFallback(filePath);
                
                string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
                string baseName = Path.GetFileNameWithoutExtension(filePath);
                string utf8Path = Path.Combine(dir, $"{baseName}_UTF8.srt");
                
                File.WriteAllText(utf8Path, text, Encoding.UTF8);
                return utf8Path;
            }
            catch
            {
                return filePath;
            }
        }

        public static string ReadFileWithEncodingFallback(string path)
        {
            if (!File.Exists(path)) return string.Empty;
            byte[] bytes = File.ReadAllBytes(path);
            return DecodeBytesToUtf8(bytes);
        }

        public static string DecodeBytesToUtf8(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;

            // 1. Check BOM
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).TrimStart('\uFEFF');
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2).TrimStart('\uFEFF');
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2).TrimStart('\uFEFF');
            }

            // 2. Check UTF-16 without BOM
            if (bytes.Length >= 4)
            {
                if (bytes[1] == 0 && bytes[3] == 0)
                {
                    try
                    {
                        string candidate = Encoding.Unicode.GetString(bytes);
                        if (candidate.Contains("-->")) return candidate;
                    }
                    catch { }
                }
                else if (bytes[0] == 0 && bytes[2] == 0)
                {
                    try
                    {
                        string candidate = Encoding.BigEndianUnicode.GetString(bytes);
                        if (candidate.Contains("-->")) return candidate;
                    }
                    catch { }
                }
            }

            // 3. Score UTF-8 candidate vs Windows-1256 candidate
            string? utf8String = null;
            try
            {
                var utf8Strict = new UTF8Encoding(false, true);
                utf8String = utf8Strict.GetString(bytes);
            }
            catch
            {
                utf8String = null;
            }

            string win1256String = DecodeWindows1256(bytes);

            if (utf8String != null)
            {
                int utf8Score = EvaluateTextQuality(utf8String);
                int win1256Score = EvaluateTextQuality(win1256String);

                // If Windows-1256 candidate has a noticeably higher Persian quality score, choose Windows-1256
                if (win1256Score > utf8Score + 3)
                {
                    return win1256String;
                }

                // Check for Mojibake in UTF-8
                int mojibakeCount = CountMojibakeMarkers(utf8String);
                if (mojibakeCount > 5)
                {
                    try
                    {
                        byte[] rawIso = Encoding.Latin1.GetBytes(utf8String);
                        string recovered = DecodeWindows1256(rawIso);
                        if (EvaluateTextQuality(recovered) > utf8Score)
                        {
                            return recovered;
                        }
                    }
                    catch { }
                }

                return utf8String;
            }

            // 4. If UTF-8 failed, return Windows-1256 decoded text
            return win1256String;
        }

        public static string DecodeWindows1256(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length);
            foreach (byte b in bytes)
            {
                int u = b;
                if (u < 0x80)
                {
                    sb.Append((char)u);
                }
                else
                {
                    char mapped = WIN1256_PERSIAN_MAP[u - 0x80];
                    char finalChar = mapped switch
                    {
                        '\u064A' => '\u06CC', // Arabic Yeh (ي) -> Persian Yeh (ی)
                        '\u0649' => '\u06CC', // Alef Maksura -> Persian Yeh (ی)
                        '\u0643' => '\u06A9', // Arabic Kaf (ك) -> Persian Kaf (ک)
                        _ => mapped
                    };
                    sb.Append(finalChar);
                }
            }
            return sb.ToString();
        }

        private static readonly string[] COMMON_PERSIAN_WORDS = new[]
        {
            "این", "از", "به", "که", "با", "برای", "یک", "است", "بود", "شد",
            "دارد", "گفت", "می", "نمی", "باشه", "هم", "رو", "چی", "چرا", "سلام",
            "خوب", "بله", "نه", "آره", "من", "تو", "او", "ما", "شما", "آنها",
            "اون", "اینجا", "اونجا", "چیزی", "کسی", "کرد", "کن", "باش", "دارم",
            "داری", "داریم", "داشته", "باشی", "باشیم", "باشند", "هست", "هستم",
            "هستی", "هستیم", "هستند", "چطور", "خیلی", "همه", "هیچ", "دیگه", "باید",
            "شاید", "فقط", "الان", "حالا", "هنوز", "بعد", "قبل", "پیش", "دوست",
            "لطفا", "ممنون", "مرسی", "کجا", "کی", "کدام", "کدوم", "چند", "چقدر"
        };

        private static int EvaluateTextQuality(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return -100;

            int score = 0;

            // 1. Structure score (Subtitles have timecodes)
            if (text.Contains("-->")) score += 20;

            // 2. Count common Persian words (high confidence)
            foreach (var word in COMMON_PERSIAN_WORDS)
            {
                // Word boundary match
                int idx = 0;
                while ((idx = text.IndexOf(word, idx, StringComparison.Ordinal)) != -1)
                {
                    bool leftOk = (idx == 0 || char.IsWhiteSpace(text[idx - 1]) || char.IsPunctuation(text[idx - 1]));
                    int rightIdx = idx + word.Length;
                    bool rightOk = (rightIdx >= text.Length || char.IsWhiteSpace(text[rightIdx]) || char.IsPunctuation(text[rightIdx]));
                    if (leftOk && rightOk)
                    {
                        score += 8;
                    }
                    idx += word.Length;
                }
            }

            // 3. Count Persian characters
            int persianLetterCount = 0;
            int weirdCharCount = 0;
            int mojibakeCount = 0;

            foreach (char ch in text)
            {
                // Standard Persian/Arabic letters
                if ((ch >= '\u0620' && ch <= '\u064A') || ch == '\u067E' || ch == '\u0686' || ch == '\u0698' || ch == '\u06AF' || ch == '\u06CC' || ch == '\u06A9')
                {
                    persianLetterCount++;
                }
                // Pashto / Urdu specific weird characters that appear when Windows-1256 is wrongly decoded as UTF-8
                else if (ch == '\u0679' || ch == '\u0688' || ch == '\u0691' || ch == '\u069F' || ch == '\u06BA' || ch == '\u06D2' || ch == '\u06C1' || ch == '\u06D5')
                {
                    weirdCharCount++;
                }
                // Mojibake Latin-1 characters
                else if (ch == 'Ø' || ch == 'Ù' || ch == 'Ú' || ch == 'Û' ||
                         ch == 'Â' || ch == 'Ã' || ch == 'Ä' || ch == 'Å' ||
                         ch == 'Æ' || ch == 'Ç' || ch == 'È' || ch == 'É' ||
                         ch == 'Ê' || ch == 'Ë' || ch == 'Ì' || ch == 'Í')
                {
                    mojibakeCount++;
                }
            }

            score += Math.Min(persianLetterCount / 5, 200);
            score -= weirdCharCount * 15; // Heavy penalty for Pashto/Urdu anomalies
            score -= mojibakeCount * 10;   // Heavy penalty for Mojibake

            return score;
        }

        private static int CountArabicCharacters(string text)
        {
            int count = 0;
            foreach (char ch in text)
            {
                if ((ch >= '\u0600' && ch <= '\u06FF') ||
                    (ch >= '\u0750' && ch <= '\u077F') ||
                    (ch >= '\uFB50' && ch <= '\uFDFF') ||
                    (ch >= '\uFE70' && ch <= '\uFEFF'))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountMojibakeMarkers(string text)
        {
            int count = 0;
            foreach (char ch in text)
            {
                if (ch == 'Ø' || ch == 'Ù' || ch == 'Ú' || ch == 'Û' ||
                    ch == 'Â' || ch == 'Ã' || ch == 'Ä' || ch == 'Å' ||
                    ch == 'Æ' || ch == 'Ç' || ch == 'È' || ch == 'É' ||
                    ch == 'Ê' || ch == 'Ë')
                {
                    count++;
                }
            }
            return count;
        }

        public static string CleanSubtitleText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            try
            {
                // 1. Remove ASS override tags like {\an8}, {\pos(100,200)}, {\c&H...}, {\fs24}, {\b1}, {\i1}, etc.
                string cleaned = Regex.Replace(text, @"\{[^}]*\}", string.Empty);

                // 2. Remove ASS dialogue line prefixes if present (e.g. "0,0:00:00.00,...")
                if (cleaned.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = cleaned.Split(new[] { ',' }, 10);
                    if (parts.Length == 10)
                    {
                        cleaned = parts[9];
                    }
                }

                // 3. Replace <br> or \N or \n with newline
                cleaned = Regex.Replace(cleaned, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
                cleaned = cleaned.Replace("\\N", "\n").Replace("\\n", "\n");

                // 4. Remove all HTML / XML tags like <font color="...">, </font>, <i>, </i>, <b>, </b>, <u>, </u>, etc.
                cleaned = Regex.Replace(cleaned, @"<[^>]+>", string.Empty);

                // 5. Decode HTML entities (&rlm;, &lrm;, &nbsp;, &amp;, &quot;)
                cleaned = System.Net.WebUtility.HtmlDecode(cleaned);
                cleaned = cleaned.Replace("\u200E", "").Replace("\u200F", "").Replace("\u200B", "");

                // 6. Clean empty lines or multiple consecutive newlines
                var lines = cleaned.Split('\n')
                                   .Select(l => l.Trim())
                                   .Where(l => !string.IsNullOrWhiteSpace(l));

                return string.Join("\n", lines).Trim();
            }
            catch
            {
                return text.Trim();
            }
        }

        private static List<SubtitleCue> ParseSrtCues(string[] lines)
        {
            var cues = new List<SubtitleCue>();
            string currentIndex = "";
            string currentTimecode = "";
            var currentText = new StringBuilder();

            void AddCurrentCue()
            {
                if (!string.IsNullOrEmpty(currentTimecode) && currentText.Length > 0)
                {
                    var (startMs, endMs) = ParseTimecodeRange(currentTimecode);
                    string raw = currentText.ToString().Trim();
                    string clean = CleanSubtitleText(raw);
                    if (!string.IsNullOrWhiteSpace(clean))
                    {
                        // 🎯 Guard against corrupt or advertising cues with absurd durations (e.g. 50 minutes long!)
                        if (endMs > startMs)
                        {
                            long duration = endMs - startMs;
                            if (duration > 12000)
                            {
                                endMs = startMs + 6000;
                            }
                        }
                        else
                        {
                            endMs = startMs + 4000;
                        }

                        cues.Add(new SubtitleCue
                        {
                            Index = currentIndex,
                            Timecode = currentTimecode,
                            Text = clean,
                            StartMs = startMs,
                            EndMs = endMs
                        });
                    }
                    currentIndex = "";
                    currentTimecode = "";
                    currentText.Clear();
                }
            }

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    AddCurrentCue();
                }
                else if (line.Contains("-->"))
                {
                    currentTimecode = line;
                }
                else if (string.IsNullOrEmpty(currentTimecode) && line.All(char.IsDigit))
                {
                    currentIndex = line;
                }
                else
                {
                    if (currentText.Length > 0) currentText.Append("\n");
                    currentText.Append(line);
                }
            }

            AddCurrentCue();
            return cues;
        }

        private static (long startMs, long endMs) ParseTimecodeRange(string timecodeLine)
        {
            try
            {
                var parts = timecodeLine.Split(new[] { "-->" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    long start = ParseSingleTimecodeToMs(parts[0].Trim());
                    long end = ParseSingleTimecodeToMs(parts[1].Trim());
                    return (start, end);
                }
            }
            catch { }
            return (0, 0);
        }

        private static long ParseSingleTimecodeToMs(string part)
        {
            try
            {
                var clean = part.Replace(',', '.');
                var pieces = clean.Split(':');
                if (pieces.Length == 3)
                {
                    if (double.TryParse(pieces[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double h) &&
                        double.TryParse(pieces[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double m) &&
                        double.TryParse(pieces[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double s))
                    {
                        return (long)((h * 3600 + m * 60 + s) * 1000);
                    }
                }
                else if (pieces.Length == 2)
                {
                    if (double.TryParse(pieces[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double m) &&
                        double.TryParse(pieces[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double s))
                    {
                        return (long)((m * 60 + s) * 1000);
                    }
                }
            }
            catch { }
            return 0;
        }

        private static async Task<string> TranslateTextBatchAsync(string text, string targetLang, System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            try
            {
                string encodedQuery = Uri.EscapeDataString(text);
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLang}&dt=t&q={encodedQuery}";

                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResult = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(jsonResult);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var segments = root[0];
                        if (segments.ValueKind == JsonValueKind.Array)
                        {
                            var sb = new StringBuilder();
                            foreach (var segment in segments.EnumerateArray())
                            {
                                if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0)
                                {
                                    if (segment[0].ValueKind == JsonValueKind.String)
                                    {
                                        sb.Append(segment[0].GetString());
                                    }
                                }
                            }
                            return sb.ToString().Trim();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LoggerService.Error("Batch translation request failed", ex);
            }

            return text;
        }

        private static Dictionary<int, string> ParseNumberedLines(string translatedText)
        {
            var resultMap = new Dictionary<int, string>();
            var lines = translatedText.Split('\n');
            int currentNum = -1;
            var currentSb = new StringBuilder();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                var match = Regex.Match(trimmed, @"^(\d+)[\.\)]\s*(.*)");
                if (match.Success)
                {
                    if (currentNum != -1)
                    {
                        resultMap[currentNum] = currentSb.ToString().Trim();
                        currentSb.Clear();
                    }
                    if (int.TryParse(match.Groups[1].Value, out int parsedNum))
                    {
                        currentNum = parsedNum;
                        currentSb.Append(match.Groups[2].Value);
                    }
                }
                else if (currentNum != -1)
                {
                    if (currentSb.Length > 0) currentSb.Append(" ");
                    currentSb.Append(trimmed);
                }
            }

            if (currentNum != -1 && currentSb.Length > 0)
            {
                resultMap[currentNum] = currentSb.ToString().Trim();
            }

            return resultMap;
        }
    }
}
