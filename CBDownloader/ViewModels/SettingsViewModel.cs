using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CBDownloader.Services;
using WinForms = System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace CBDownloader.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "CBDownloader";

        [ObservableProperty]
        private bool _startWithWindows;

        [ObservableProperty]
        private string _updateStatus = "Ready";

        [ObservableProperty]
        private string _currentVersion;

        [ObservableProperty]
        private string _downloadFolderPath;

        [ObservableProperty]
        private bool _useBrowserCookies;

        [ObservableProperty]
        private string _browserForCookies;

        [ObservableProperty]
        private string _appTheme;
        
        [ObservableProperty]
        private bool _alwaysOnTop;

        public ObservableCollection<string> AvailableBrowsers { get; } = new ObservableCollection<string> { "edge", "chrome", "firefox", "opera", "opera:gx", "brave", "vivaldi" };
        public ObservableCollection<string> AvailableThemes { get; } = new ObservableCollection<string> { "System", "Light", "Dark" };

        public SettingsViewModel()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            CurrentVersion = $"v{version?.Major}.{version?.Minor}.{version?.Build}";
            StartWithWindows = CheckIfAutoStartEnabled();

            DownloadFolderPath = SettingsService.Current.DownloadFolderPath;
            UseBrowserCookies = SettingsService.Current.UseBrowserCookies;
            BrowserForCookies = SettingsService.Current.BrowserForCookies;
            AppTheme = SettingsService.Current.AppTheme;
            AlwaysOnTop = SettingsService.Current.AlwaysOnTop;
        }

        partial void OnAlwaysOnTopChanged(bool value)
        {
            SettingsService.Current.AlwaysOnTop = value;
            SettingsService.Save();
            
            // Apply immediately to current instance
            if (System.Windows.Application.Current.MainWindow != null)
            {
                System.Windows.Application.Current.MainWindow.Topmost = value;
            }
        }

        partial void OnAppThemeChanged(string value)
        {
            SettingsService.Current.AppTheme = value;
            SettingsService.Save();
            ThemeManager.ApplyTheme(value);
        }

        partial void OnDownloadFolderPathChanged(string value)
        {
            SettingsService.Current.DownloadFolderPath = value;
            SettingsService.Save();
        }

        partial void OnUseBrowserCookiesChanged(bool value)
        {
            SettingsService.Current.UseBrowserCookies = value;
            SettingsService.Save();
        }

        partial void OnBrowserForCookiesChanged(string value)
        {
            SettingsService.Current.BrowserForCookies = value;
            SettingsService.Save();
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select Download Folder",
                SelectedPath = DownloadFolderPath
            };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                DownloadFolderPath = dialog.SelectedPath;
            }
        }

        partial void OnStartWithWindowsChanged(bool value)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
                if (key != null)
                {
                    if (value)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key.SetValue(AppName, $"\"{exePath}\"");
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Failed to set startup: {ex.Message}";
            }
        }

        private static readonly HttpClient SharedHttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "ClipBoardDownloader-App");
            return client;
        }

        private static bool IsNewerVersion(string remoteTag, string localTag)
        {
            if (Version.TryParse(remoteTag.TrimStart('v', 'V'), out var remoteVer) &&
                Version.TryParse(localTag.TrimStart('v', 'V'), out var localVer))
            {
                return remoteVer > localVer;
            }
            return !string.Equals(remoteTag, localTag, StringComparison.OrdinalIgnoreCase);
        }

        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            UpdateStatus = "Checking for updates...";
            try
            {
                const string url = "https://api.github.com/repos/vibecastle/ClipBoardDownloader/releases/latest";
                using var response = await SharedHttpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GitHubRelease>();
                    if (result != null && !string.IsNullOrEmpty(result.tag_name))
                    {
                        if (IsNewerVersion(result.tag_name, CurrentVersion))
                        {
                            UpdateStatus = $"Update available: {result.tag_name}!";
                            var asset = result.assets.FirstOrDefault(a => a.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                            if (asset != null)
                            {
                                var confirm = System.Windows.MessageBox.Show(
                                    $"A new version ({result.tag_name}) is available. Wish to update now?",
                                    "Update Available",
                                    System.Windows.MessageBoxButton.YesNo,
                                    System.Windows.MessageBoxImage.Question);

                                if (confirm == System.Windows.MessageBoxResult.Yes)
                                {
                                    await DownloadAndInstallUpdate(asset.browser_download_url, asset.name);
                                }
                            }
                            else
                            {
                                // Fallback to opening browser if no EXE found
                                OpenBrowser(result.html_url);
                            }
                        }
                        else
                        {
                            UpdateStatus = "You are up to date!";
                        }
                    }
                    else
                    {
                        UpdateStatus = "Could not parse release info.";
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    UpdateStatus = "You are up to date! (No online releases yet)";
                }
                else
                {
                    UpdateStatus = $"Check failed: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Error: {ex.Message}";
            }
        }

        private async Task DownloadAndInstallUpdate(string url, string fileName)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);

                using var response = await SharedHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                var totalRead = 0L;
                int bytesRead;
                int lastProgress = -1;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;

                    if (canReportProgress)
                    {
                        int progress = (int)((totalRead * 100) / totalBytes);
                        if (progress != lastProgress)
                        {
                            lastProgress = progress;
                            UpdateStatus = $"Downloading: {progress}%";
                        }
                    }
                }
                
                fileStream.Close();

                UpdateStatus = "Finalizing update...";
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/SILENT",
                    UseShellExecute = true
                });

                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Failed to download update: {ex.Message}";
            }
        }

        private bool CheckIfAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
                var value = key?.GetValue(AppName);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        private void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private class GitHubRelease
        {
            public string tag_name { get; set; } = string.Empty;
            public string html_url { get; set; } = string.Empty;
            public List<GitHubAsset> assets { get; set; } = new();
        }

        private class GitHubAsset
        {
            public string name { get; set; } = string.Empty;
            public string browser_download_url { get; set; } = string.Empty;
        }
    }
}
