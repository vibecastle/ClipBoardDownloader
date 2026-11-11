using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CBDownloader.Models;
using CBDownloader.Services;
using YoutubeDLSharp;

namespace CBDownloader.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly MediaDownloadService _mediaService;
        private System.Threading.CancellationTokenSource? _metadataCts;
        private bool _hasValidMetadata;

        public ObservableCollection<DownloadItemViewModel> Downloads { get; } = new ObservableCollection<DownloadItemViewModel>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DownloadVideoCommand))]
        [NotifyCanExecuteChangedFor(nameof(DownloadAudioCommand))]
        private bool _isBusy;

        [ObservableProperty]
        private string _videoUrl = string.Empty;

        [ObservableProperty]
        private string _videoTitle = "Waiting for link...";

        [ObservableProperty]
        private string _videoThumbnailUrl = string.Empty;

        public bool IsTopmost => SettingsService.Current.AlwaysOnTop;

        public static MainViewModel? Current { get; private set; }

        public MainViewModel(MediaDownloadService? mediaService = null)
        {
            Current = this;
            _mediaService = mediaService ?? new MediaDownloadService();
            _ = _mediaService.EnsureBinariesExist();
        }

        public async Task InitializeAndFetchMetadata(string url)
        {
            url = Utils.RegexHelper.NormalizeUrl(url);
            if (string.IsNullOrWhiteSpace(url)) return;

            if (_hasValidMetadata && !IsBusy && string.Equals(VideoUrl, url, StringComparison.Ordinal))
            {
                return;
            }

            _metadataCts?.Cancel();
            _metadataCts?.Dispose();
            var cts = new System.Threading.CancellationTokenSource();
            _metadataCts = cts;
            var token = cts.Token;

            VideoUrl = url;
            VideoTitle = "Fetching media information...";
            VideoThumbnailUrl = string.Empty;
            _hasValidMetadata = false;
            IsBusy = true;

            try
            {
                await _mediaService.EnsureBinariesExist();
                token.ThrowIfCancellationRequested();

                var metadata = await _mediaService.GetVideoMetadataAsync(url, token);
                if (token.IsCancellationRequested) return;

                VideoTitle = !string.IsNullOrWhiteSpace(metadata.Title) ? metadata.Title : "Media Ready";
                VideoThumbnailUrl = metadata.Thumbnail ?? string.Empty;
                _hasValidMetadata = true;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                if (!token.IsCancellationRequested)
                {
                    VideoTitle = "Ready for links";
                    _hasValidMetadata = false;
                }
            }
            finally
            {
                if (_metadataCts == cts)
                {
                    IsBusy = false;
                }
            }
        }

        private bool CanDownload() => !IsBusy && _hasValidMetadata && !string.IsNullOrWhiteSpace(VideoUrl);

        [RelayCommand(CanExecute = nameof(CanDownload))]
        private void DownloadVideo() => AddDownloadToQueue(true);

        [RelayCommand(CanExecute = nameof(CanDownload))]
        private void DownloadAudio() => AddDownloadToQueue(false);

        private void AddDownloadToQueue(bool isVideo)
        {
            if (string.IsNullOrWhiteSpace(VideoUrl) || IsBusy) return;

            if (Utils.RegexHelper.IsYoutubePurePlaylistUrl(VideoUrl))
            {
                ((App)System.Windows.Application.Current).ShowPlaylistWindow(_mediaService, VideoUrl, VideoTitle, isVideo);
                return;
            }

            if (Utils.RegexHelper.IsYoutubePlaylistUrl(VideoUrl))
            {
                var result = System.Windows.MessageBox.Show(
                    "This link is part of a playlist.\nDownload the full playlist?",
                    "Playlist Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ((App)System.Windows.Application.Current).ShowPlaylistWindow(_mediaService, VideoUrl, VideoTitle, isVideo);
                    return;
                }
            }

            EnqueueSingleItem(VideoTitle, VideoThumbnailUrl, VideoUrl, isVideo);
        }

        public void QueueDirect(string url, bool isVideo, string? title = null, string? thumbnailUrl = null)
        {
            url = Utils.RegexHelper.NormalizeUrl(url);
            if (string.IsNullOrWhiteSpace(url)) return;

            string itemTitle = !string.IsNullOrWhiteSpace(title) && title != "Fetching info..." && title != "Fetching link..."
                ? title
                : (isVideo ? "Video Download" : "Audio Download");

            EnqueueSingleItem(itemTitle, thumbnailUrl ?? string.Empty, url, isVideo);
            ((App)System.Windows.Application.Current).ShowNotification("Download Queued", $"{(isVideo ? "MP4" : "MP3")}: {itemTitle}");
        }

        private void EnqueueSingleItem(string title, string thumbnailUrl, string url, bool isVideo, string? playlistName = null)
        {
            var newItem = new DownloadItemViewModel(_mediaService)
            {
                VideoTitle = title,
                VideoThumbnailUrl = thumbnailUrl,
                VideoUrl = url,
                IsVideo = isVideo,
                PlaylistName = playlistName
            };

            Downloads.Add(newItem);
            _ = newItem.StartDownloadAsync();
        }

        public void AddPlaylistItemsToQueue(List<PlaylistItemModel> items, bool isVideo, string? playlistName = null)
        {
            foreach (var item in items)
            {
                EnqueueSingleItem(item.Title, item.ThumbnailUrl, item.VideoUrl, isVideo, playlistName);
            }
        }

        private void ClearPreview()
        {
            VideoUrl = string.Empty;
            VideoTitle = "Waiting for link...";
            VideoThumbnailUrl = string.Empty;
        }

        [RelayCommand]
        private void Exit()
        {
            foreach (var dl in Downloads)
            {
                if (dl.IsDownloading) dl.CancelCommand.Execute(null);
            }
            System.Windows.Application.Current.MainWindow?.Hide();
        }

        [RelayCommand]
        private void ClearCompleted()
        {
            var itemsToRemove = new List<DownloadItemViewModel>();
            foreach (var item in Downloads)
            {
                if (!item.IsDownloading && (item.IsCompleted || item.DownloadStatus.Contains("Error") || item.DownloadStatus == "Cancelled."))
                {
                    itemsToRemove.Add(item);
                }
            }
            foreach (var item in itemsToRemove)
            {
                Downloads.Remove(item);
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            ((App)System.Windows.Application.Current).ShowSettings();
        }
    }
}
