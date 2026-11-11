using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CBDownloader.Models;
using CBDownloader.Services;

namespace CBDownloader.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly MediaDownloadService _mediaService;

        public ObservableCollection<DownloadItemViewModel> Downloads { get; } = new();

        public bool HasNoDownloads => Downloads.Count == 0;

        public bool IsTopmost => SettingsService.Current.AlwaysOnTop;

        public static MainViewModel? Current { get; private set; }

        public MainViewModel(MediaDownloadService? mediaService = null)
        {
            Current = this;
            _mediaService = mediaService ?? new MediaDownloadService();
            _ = _mediaService.EnsureBinariesExist();

            Downloads.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasNoDownloads));
        }

        public void QueueDirect(string url, bool isVideo, string? title = null, string? thumbnailUrl = null)
        {
            url = Utils.RegexHelper.NormalizeUrl(url);
            if (string.IsNullOrWhiteSpace(url)) return;

            if (Utils.RegexHelper.IsYoutubePurePlaylistUrl(url))
            {
                ((App)System.Windows.Application.Current).ShowPlaylistWindow(_mediaService, url, title ?? "Playlist", isVideo);
                return;
            }

            if (Utils.RegexHelper.IsYoutubePlaylistUrl(url))
            {
                var result = System.Windows.MessageBox.Show(
                    "This link is part of a playlist.\nDownload the full playlist?",
                    "Playlist Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ((App)System.Windows.Application.Current).ShowPlaylistWindow(_mediaService, url, title ?? "Playlist", isVideo);
                    return;
                }
            }

            string itemTitle = !string.IsNullOrWhiteSpace(title) && title != "Fetching info..." && title != "Fetching link..." && title != "Media Download"
                ? title
                : "Fetching title...";

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

        [RelayCommand]
        private void OpenFolder()
        {
            try
            {
                var folder = SettingsService.Current.DownloadFolderPath;
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch { }
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
