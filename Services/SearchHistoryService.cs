using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MovieManagerDesktop.Services
{
    public static class SearchHistoryService
    {
        private static readonly string _historyFile;
        private const int MaxHistory = 10;

        static SearchHistoryService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string cineTrackDir = Path.Combine(appData, "CineTrack");
            if (!Directory.Exists(cineTrackDir)) Directory.CreateDirectory(cineTrackDir);
            
            _historyFile = Path.Combine(cineTrackDir, "search_history.json");
        }

        public static List<string> GetHistory()
        {
            if (!File.Exists(_historyFile)) return new List<string>();

            try
            {
                var json = File.ReadAllText(_historyFile);
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static void AddSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;

            var history = GetHistory();
            
            // Remove if exists to bring it to top
            history.Remove(query);
            history.Insert(0, query);

            if (history.Count > MaxHistory)
            {
                history = history.Take(MaxHistory).ToList();
            }

            try
            {
                File.WriteAllText(_historyFile, JsonSerializer.Serialize(history));
            }
            catch { }
        }

        public static void ClearHistory()
        {
            if (File.Exists(_historyFile))
            {
                File.Delete(_historyFile);
            }
        }
    }
}
