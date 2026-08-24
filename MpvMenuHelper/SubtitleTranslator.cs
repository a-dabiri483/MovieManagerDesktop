using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MpvMenuHelper
{
    public class SubtitleCue
    {
        public string Index { get; set; } = "";
        public string Timecode { get; set; } = "";
        public string Text { get; set; } = "";
    }

    public static class SubtitleTranslator
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        public static async Task<string> TranslateSrtFileAsync(
            string originalFilePath,
            string targetLang = "fa",
            IProgress<(int current, int total, string status)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(originalFilePath))
            {
                throw new FileNotFoundException("فایل زیرنویس یافت نشد: " + originalFilePath);
            }

            var lines = await File.ReadAllLinesAsync(originalFilePath, Encoding.UTF8, cancellationToken);
            var cues = ParseSrtCues(lines);

            if (cues.Count == 0)
            {
                throw new InvalidDataException("فرمت فایل زیرنویس معتبر نیست یا زیرنویس خالی است.");
            }

            int batchSize = 15;
            int totalBatches = (cues.Count + batchSize - 1) / batchSize;
            var translatedCues = new List<SubtitleCue>();

            for (int i = 0; i < cues.Count; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int currentBatchIndex = (i / batchSize) + 1;
                progress?.Report((currentBatchIndex, totalBatches, $"در حال ترجمه دسته {currentBatchIndex} از {totalBatches}..."));

                int count = Math.Min(batchSize, cues.Count - i);
                var batch = cues.GetRange(i, count);

                var sb = new StringBuilder();
                for (int bIdx = 0; bIdx < batch.Count; bIdx++)
                {
                    string cleanText = batch[bIdx].Text.Replace("\r", "").Replace("\n", " ");
                    sb.AppendLine($"{bIdx + 1}. {cleanText}");
                }

                string translatedBatchText = await TranslateBatchAsync(sb.ToString().Trim(), targetLang, cancellationToken);
                var translatedMap = ParseNumberedLines(translatedBatchText);

                for (int bIdx = 0; bIdx < batch.Count; bIdx++)
                {
                    string transText = translatedMap.TryGetValue(bIdx + 1, out var val) && !string.IsNullOrWhiteSpace(val)
                        ? val
                        : batch[bIdx].Text;

                    translatedCues.Add(new SubtitleCue
                    {
                        Index = batch[bIdx].Index,
                        Timecode = batch[bIdx].Timecode,
                        Text = transText
                    });
                }

                await Task.Delay(100, cancellationToken); // Rate limit protection
            }

            // Save to temp or adjacent directory
            string dir = Path.GetDirectoryName(originalFilePath) ?? Path.GetTempPath();
            string fileName = Path.GetFileNameWithoutExtension(originalFilePath) + "_FA.srt";
            string outputPath = Path.Combine(dir, fileName);

            try
            {
                // Test write permissions
                using (File.Create(outputPath)) { }
            }
            catch
            {
                // Fallback to temp
                outputPath = Path.Combine(Path.GetTempPath(), fileName);
            }

            var srtBuilder = new StringBuilder();
            for (int idx = 0; idx < translatedCues.Count; idx++)
            {
                srtBuilder.AppendLine((idx + 1).ToString());
                srtBuilder.AppendLine(translatedCues[idx].Timecode);
                srtBuilder.AppendLine(translatedCues[idx].Text);
                srtBuilder.AppendLine();
            }

            await File.WriteAllTextAsync(outputPath, srtBuilder.ToString(), Encoding.UTF8, cancellationToken);
            return outputPath;
        }

        private static async Task<string> TranslateBatchAsync(string text, string targetLang, CancellationToken ct)
        {
            try
            {
                string encoded = Uri.EscapeDataString(text);
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLang}&dt=t&q={encoded}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                var sb = new StringBuilder();
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var firstArray = doc.RootElement[0];
                    if (firstArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var elem in firstArray.EnumerateArray())
                        {
                            if (elem.ValueKind == JsonValueKind.Array && elem.GetArrayLength() > 0)
                            {
                                sb.Append(elem[0].GetString());
                            }
                        }
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return text; // Fallback
            }
        }

        private static List<SubtitleCue> ParseSrtCues(string[] lines)
        {
            var cues = new List<SubtitleCue>();
            string currentIndex = "";
            string currentTimecode = "";
            var currentText = new StringBuilder();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    if (!string.IsNullOrEmpty(currentTimecode) && currentText.Length > 0)
                    {
                        cues.Add(new SubtitleCue
                        {
                            Index = currentIndex,
                            Timecode = currentTimecode,
                            Text = currentText.ToString().Trim()
                        });
                        currentIndex = "";
                        currentTimecode = "";
                        currentText.Clear();
                    }
                }
                else if (string.IsNullOrEmpty(currentIndex) && int.TryParse(line, out _))
                {
                    currentIndex = line;
                }
                else if (string.IsNullOrEmpty(currentTimecode) && line.Contains("-->"))
                {
                    currentTimecode = line;
                }
                else
                {
                    if (currentText.Length > 0) currentText.AppendLine();
                    currentText.Append(line);
                }
            }

            if (!string.IsNullOrEmpty(currentTimecode) && currentText.Length > 0)
            {
                cues.Add(new SubtitleCue
                {
                    Index = currentIndex,
                    Timecode = currentTimecode,
                    Text = currentText.ToString().Trim()
                });
            }

            return cues;
        }

        private static Dictionary<int, string> ParseNumberedLines(string text)
        {
            var map = new Dictionary<int, string>();
            var lines = text.Split('\n');
            var regex = new Regex(@"^\s*(\d+)[\.\-\:]\s*(.*)$");

            int lastIndex = -1;
            var currentText = new StringBuilder();

            foreach (var raw in lines)
            {
                string line = raw.Trim();
                var match = regex.Match(line);
                if (match.Success)
                {
                    if (lastIndex != -1)
                    {
                        map[lastIndex] = currentText.ToString().Trim();
                        currentText.Clear();
                    }
                    lastIndex = int.Parse(match.Groups[1].Value);
                    currentText.Append(match.Groups[2].Value);
                }
                else if (lastIndex != -1)
                {
                    currentText.Append(" ").Append(line);
                }
            }

            if (lastIndex != -1)
            {
                map[lastIndex] = currentText.ToString().Trim();
            }

            return map;
        }
    }
}
