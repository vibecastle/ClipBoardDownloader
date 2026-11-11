using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using CBDownloader.Models;

namespace CBDownloader.ViewModels
{
    public partial class PlaylistEntryViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        public string Title { get; init; } = string.Empty;
        public string VideoUrl { get; init; } = string.Empty;
        public string ThumbnailUrl { get; init; } = string.Empty;
        public string Duration { get; init; } = string.Empty;
        public int Index { get; init; }
    }

    public partial class PlaylistViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _playlistTitle = string.Empty;

        [ObservableProperty]
        private bool _isLoading = true;

        [ObservableProperty]
        private bool _isVideo;

        [ObservableProperty]
        private string _loadingStatus = "Fetching playlist information...";

        private bool _suppressSelectionChanged;

        public ObservableCollection<PlaylistEntryViewModel> Entries { get; } = new();

        public int SelectedCount => Entries.Count(e => e.IsSelected);
        public int TotalCount => Entries.Count;

        public bool DownloadRequested { get; private set; }

        public System.Action? CloseAction { get; set; }

        [RelayCommand]
        private void SelectAll()
        {
            _suppressSelectionChanged = true;
            try
            {
                foreach (var entry in Entries)
                    entry.IsSelected = true;
            }
            finally
            {
                _suppressSelectionChanged = false;
                OnPropertyChanged(nameof(SelectedCount));
            }
        }

        [RelayCommand]
        private void DeselectAll()
        {
            _suppressSelectionChanged = true;
            try
            {
                foreach (var entry in Entries)
                    entry.IsSelected = false;
            }
            finally
            {
                _suppressSelectionChanged = false;
                OnPropertyChanged(nameof(SelectedCount));
            }
        }

        [RelayCommand]
        private void StartDownload()
        {
            DownloadRequested = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            DownloadRequested = false;
            CloseAction?.Invoke();
        }

        public void LoadEntries(string playlistTitle, System.Collections.Generic.List<PlaylistItemModel> items)
        {
            PlaylistTitle = playlistTitle;
            Entries.Clear();

            foreach (var item in items)
            {
                var entry = new PlaylistEntryViewModel
                {
                    Index = item.Index,
                    Title = item.Title,
                    VideoUrl = item.VideoUrl,
                    ThumbnailUrl = item.ThumbnailUrl,
                    Duration = item.Duration
                };
                entry.PropertyChanged += (_, e) =>
                {
                    if (!_suppressSelectionChanged && e.PropertyName == nameof(PlaylistEntryViewModel.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedCount));
                    }
                };
                Entries.Add(entry);
            }

            IsLoading = false;
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(SelectedCount));
        }

        public System.Collections.Generic.List<PlaylistItemModel> GetSelectedItems()
        {
            return Entries
                .Where(e => e.IsSelected)
                .Select(e => new PlaylistItemModel
                {
                    Index = e.Index,
                    Title = e.Title,
                    VideoUrl = e.VideoUrl,
                    ThumbnailUrl = e.ThumbnailUrl,
                    Duration = e.Duration
                })
                .ToList();
        }
    }
}
