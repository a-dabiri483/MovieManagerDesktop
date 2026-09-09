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
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string movieManagerDir = Path.Combine(appData, "MovieManager");
            if (!Directory.Exists(movieManagerDir)) Directory.CreateDirectory(movieManagerDir);
            
            _historyFile = Path.Combine(movieManagerDir, "search_history.json");

            // Migration from legacy CineTrack folder
            string legacyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CineTrack");
            string legacyFile = Path.Combine(legacyDir, "search_history.json");
            if (!File.Exists(_historyFile) && File.Exists(legacyFile))
            {
                try { File.Copy(legacyFile, _historyFile, true); } catch { }
            }
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
