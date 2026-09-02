using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class TranslationService
    {
        private static readonly HttpClient _httpClient;

        static TranslationService()
        {
            _httpClient = new HttpClient(new MovieManagerDesktop.Services.Network.ProxyHttpClientHandler())
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            }
        }
        
        public static async Task<string> TranslateTextAsync(string text, string? targetLanguage = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            
            try
            {
                if (string.IsNullOrWhiteSpace(targetLanguage))
                {
                    var settings = SettingsManager.LoadSettings();
                    targetLanguage = settings.TranslateToLanguage;
                }

                if (string.IsNullOrWhiteSpace(targetLanguage) || targetLanguage == "auto")
                {
                    targetLanguage = "fa";
                }

                string encodedQuery = Uri.EscapeDataString(text);

                // ── Strategy 1: Google Chrome Translate API (clients5 - High reliability, no 429) ──
                try
                {
                    string url1 = $"https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl={targetLanguage}&q={encodedQuery}";
                    var resp1 = await _httpClient.GetAsync(url1);
                    if (resp1.IsSuccessStatusCode)
                    {
                        string jsonResult = await resp1.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(jsonResult);
                        var root = doc.RootElement;
                        
                        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                        {
                            var first = root[0];
                            if (first.ValueKind == JsonValueKind.Array && first.GetArrayLength() > 0)
                            {
                                string? trans = first[0].GetString();
                                if (!string.IsNullOrWhiteSpace(trans)) return trans.Trim();
                            }
                            else if (first.ValueKind == JsonValueKind.String)
                            {
                                string? trans = first.GetString();
                                if (!string.IsNullOrWhiteSpace(trans)) return trans.Trim();
                            }
                        }
                    }
                }
                catch { }

                // ── Strategy 2: Google Mobile Translate Web Scraper (/m) ──
                try
                {
                    string url2 = $"https://translate.google.com/m?sl=auto&tl={targetLanguage}&q={encodedQuery}";
                    var resp2 = await _httpClient.GetAsync(url2);
                    if (resp2.IsSuccessStatusCode)
                    {
                        string html = await resp2.Content.ReadAsStringAsync();
                        var match = Regex.Match(html, @"class=""result-container"">(.*?)</div>", RegexOptions.Singleline);
                        if (match.Success)
                        {
                            string decoded = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
                            if (!string.IsNullOrWhiteSpace(decoded)) return decoded;
                        }
                    }
                }
                catch { }

                // ── Strategy 3: MyMemory Translation API (Trusted free fallback) ──
                try
                {
                    string url3 = $"https://api.mymemory.translated.net/get?q={encodedQuery}&langpair=en|{targetLanguage}";
                    var resp3 = await _httpClient.GetAsync(url3);
                    if (resp3.IsSuccessStatusCode)
                    {
                        string json = await resp3.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("responseData", out var respData) &&
                            respData.TryGetProperty("translatedText", out var transProp))
                        {
                            string? t = transProp.GetString();
                            if (!string.IsNullOrWhiteSpace(t)) return t.Trim();
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Translation Error: {ex.Message}");
            }
            
            return text; // Fallback to original text
        }
    }
}
