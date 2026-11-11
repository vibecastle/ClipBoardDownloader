using System;
using System.IO;
using System.Text.Json;

namespace CBDownloader.Services
{
    public class Settings
    {
        public string DownloadFolderPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CBDownloader");
        public bool UseBrowserCookies { get; set; } = false;
        public string BrowserForCookies { get; set; } = GetSystemDefaultBrowser();
        public string AppTheme { get; set; } = "System";
        public bool AlwaysOnTop { get; set; } = true;

        public static string GetSystemDefaultBrowser()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice"))
                {
                    var progId = key?.GetValue("ProgId")?.ToString();
                    if (string.IsNullOrEmpty(progId)) return "edge";

                    if (progId.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) return "chrome";
                    if (progId.Contains("Edge", StringComparison.OrdinalIgnoreCase)) return "edge";
                    if (progId.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) return "firefox";
                    if (progId.Contains("Opera", StringComparison.OrdinalIgnoreCase)) 
                    {
                        return progId.Contains("GX", StringComparison.OrdinalIgnoreCase) ? "opera:gx" : "opera";
                    }
                    if (progId.Contains("Brave", StringComparison.OrdinalIgnoreCase)) return "brave";
                    return "edge";
                }
            }
            catch { return "edge"; }
        }
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
