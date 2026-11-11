using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using CBDownloader.Services;
using YoutubeDLSharp;

namespace CBDownloader.ViewModels
{
    public partial class DownloadItemViewModel : ObservableObject
    {
        private readonly MediaDownloadService _mediaService;
        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private string _videoTitle = string.Empty;

        [ObservableProperty]
        private string _videoThumbnailUrl = string.Empty;

        [ObservableProperty]
        private double _downloadProgress;

        [ObservableProperty]
        private string _downloadStatus = "Pending...";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowRetry))]
        private bool _isDownloading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowRetry))]
        private bool _isCompleted;
        
        [ObservableProperty]
        private string _videoUrl = string.Empty;

        public bool IsVideo { get; set; }
        public string? PlaylistName { get; set; }
        
        public string? FilePath { get; private set; }

        public bool ShowRetry => !IsDownloading && !IsCompleted;

        public DownloadItemViewModel(MediaDownloadService mediaService)
        {
            _mediaService = mediaService;
        }

        private bool _isPostProcessing;

        public async Task StartDownloadAsync()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            var cts = new CancellationTokenSource();
            _cts = cts;

            IsDownloading = true;
            IsCompleted = false;
            _isPostProcessing = false;
            DownloadStatus = "Starting download...";
            DownloadProgress = 0;

            long lastUiUpdateTick = 0;

            var progress = new Progress<DownloadProgress>(p =>
            {
                if (cts.IsCancellationRequested) return;

                if (p.State == DownloadState.Downloading && !_isPostProcessing)
                {
                    var prc = p.Progress * 100;
                    if (prc >= 100)
                    {
                        _isPostProcessing = true;
                        DownloadProgress = 99;
                        DownloadStatus = "Converting media (please wait)... 99.9%";
                        return;
                    }

                    long now = Environment.TickCount64;
                    if (prc - DownloadProgress >= 0.3 || now - lastUiUpdateTick >= 80)
                    {
                        lastUiUpdateTick = now;
                        if (prc > DownloadProgress)
                        {
                            DownloadProgress = prc;
                        }
                        DownloadStatus = $"Downloading... {DownloadProgress:F1}% ({p.DownloadSpeed})";
                    }
                }
                else if (p.State == DownloadState.PostProcessing)
                {
                    if (!_isPostProcessing)
                    {
                        _isPostProcessing = true;
                        DownloadProgress = 99;
                        DownloadStatus = "Converting media (please wait)... 99.0%";
                    }
                }
            });

            try
            {
                var result = await _mediaService.DownloadAsync(VideoUrl, IsVideo, true, progress, cts.Token, PlaylistName);
                _isPostProcessing = false;
                
                if (cts.IsCancellationRequested)
                {
                    DownloadStatus = "Cancelled.";
                    return;
                }
                
                if (result.Success)
                {
                    FilePath = result.Data;
                    DownloadStatus = "Completed!";
                    DownloadProgress = 100;
                    IsCompleted = true;
                    
                    ((App)System.Windows.Application.Current).ShowNotification("Download Completed!", VideoTitle);
                }
                else
                {
                    DownloadStatus = $"Error: {string.Join(" ", result.ErrorOutput)}";
                }
            }
            catch (OperationCanceledException)
            {
                _isPostProcessing = false;
                DownloadStatus = "Cancelled.";
            }
            catch (Exception ex)
            {
                _isPostProcessing = false;
                if (cts.IsCancellationRequested)
                {
                    DownloadStatus = "Cancelled.";
                }
                else
                {
                    DownloadStatus = $"Error: {ex.Message}";
                }
            }
            finally
            {
                _isPostProcessing = false;
                if (_cts == cts)
                {
                    IsDownloading = false;
                }
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (IsDownloading)
            {
                _cts?.Cancel();
                DownloadStatus = "Cancelled.";
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        [RelayCommand]
        private void RetryVideo()
        {
            IsVideo = true;
            _ = StartDownloadAsync();
        }

        [RelayCommand]
        private void RetryAudio()
        {
            IsVideo = false;
            _ = StartDownloadAsync();
        }

        [RelayCommand]
        private void OpenFolder()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            if (System.IO.File.Exists(FilePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{FilePath}\"",
                    UseShellExecute = true
                });
                return;
            }

            var folder = System.IO.Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folder}\"",
                    UseShellExecute = true
                });
            }
        }
    }
}
