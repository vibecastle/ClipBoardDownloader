using System;
using System.IO;
using System.Text.Json;

namespace CBDownloader.Services
{
    public class Settings
    {
        public string DownloadFolderPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CBDownloader");
        public string AppTheme { get; set; } = "System";
        public bool AlwaysOnTop { get; set; } = true;
    }

    public static class SettingsService
    {
        private static readonly string SettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CBDownloader", "appsettings.json");
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
        private static Settings? _current;

        public static Settings Current
        {
            get
            {
                if (_current == null)
                    Load();
                return _current!;
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    _current = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
                else
                {
                    _current = new Settings();
                }
            }
            catch
            {
                _current = new Settings();
            }
        }

        public static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var json = JsonSerializer.Serialize(Current, SerializerOptions);
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }
}
