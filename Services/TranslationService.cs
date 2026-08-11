using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    public class TranslationService
    {
        private static readonly HttpClient _httpClient = new HttpClient(new MovieManagerDesktop.Services.Network.ProxyHttpClientHandler());
        
        public static async Task<string> TranslateTextAsync(string text, string targetLanguage = "fa")
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            
            try
            {
                string encodedQuery = Uri.EscapeDataString(text);
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLanguage}&dt=t&q={encodedQuery}";
                
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResult = await response.Content.ReadAsStringAsync();
                    
                    // The Google Translate API returns an array of arrays.
                    // Example: [[["سلام دنیا","Hello world",null,null,1]],null,"en",null,null,null,1,[],[["en"],1,true],1,["en"]]
                    using var doc = JsonDocument.Parse(jsonResult);
                    var root = doc.RootElement;
                    
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var segments = root[0];
                        if (segments.ValueKind == JsonValueKind.Array)
                        {
                            string translatedText = "";
                            foreach (var segment in segments.EnumerateArray())
                            {
                                if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0)
                                {
                                    if (segment[0].ValueKind == JsonValueKind.String)
                                    {
                                        translatedText += segment[0].GetString();
                                    }
                                }
                            }
                            return translatedText.Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Translation Error: {ex.Message}");
            }
            
            return text; // Fallback to original text
        }
    }
}
