using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CBDownloader.Services;
using CBDownloader.ViewModels;

namespace CBDownloader.Views
{
    public partial class DownloadToastWindow : Window
    {
        private const int TotalDurationMs = 5000;
        private const int TickIntervalMs = 50;

        private readonly MediaDownloadService _mediaService;
        private readonly DispatcherTimer _timer;
        private int _elapsedMs;
        private bool _isPaused;
        private CancellationTokenSource? _metadataCts;

        public string CurrentUrl { get; private set; } = string.Empty;
        public string FetchedTitle { get; private set; } = string.Empty;
        public string FetchedThumbnailUrl { get; private set; } = string.Empty;

        public DownloadToastWindow(MediaDownloadService mediaService, string url)
        {
            InitializeComponent();
            _mediaService = mediaService;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(TickIntervalMs)
            };
            _timer.Tick += OnTimerTick;

            PositionInBottomRight();
            SetUrlAndFetch(url);
        }

        public void PositionInBottomRight()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 16;
            Top = workArea.Bottom - Height - 16;
        }

        public void SetUrlAndFetch(string url)
        {
            CurrentUrl = url;
            FetchedTitle = string.Empty;
            FetchedThumbnailUrl = string.Empty;

            TitleText.Text = "Fetching info...";
            ThumbnailImage.Source = null;

            _elapsedMs = 0;
            CountdownBar.Value = 100;
            _isPaused = false;

            _timer.Stop();
            _timer.Start();

            _metadataCts?.Cancel();
            _metadataCts?.Dispose();
            _metadataCts = new CancellationTokenSource();
            var token = _metadataCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _mediaService.EnsureBinariesExist();
                    if (token.IsCancellationRequested) return;

                    var metadata = await _mediaService.GetVideoMetadataAsync(url, token);
                    if (token.IsCancellationRequested) return;

                    Dispatcher.Invoke(() =>
                    {
                        FetchedTitle = metadata.Title ?? "Ready to download";
                        FetchedThumbnailUrl = metadata.Thumbnail ?? string.Empty;
                        TitleText.Text = FetchedTitle;

                        if (!string.IsNullOrWhiteSpace(FetchedThumbnailUrl))
                        {
                            try
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.UriSource = new Uri(FetchedThumbnailUrl);
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.EndInit();
                                ThumbnailImage.Source = bmp;
                            }
                            catch { }
                        }
                    });
                }
                catch (OperationCanceledException) { }
                catch
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (!token.IsCancellationRequested && string.IsNullOrEmpty(FetchedTitle))
                        {
                            TitleText.Text = "Link Ready";
                        }
                    });
                }
            }, token);
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_isPaused) return;

            _elapsedMs += TickIntervalMs;
            double remainingPercent = Math.Max(0, (1.0 - ((double)_elapsedMs / TotalDurationMs)) * 100);
            CountdownBar.Value = remainingPercent;

            if (_elapsedMs >= TotalDurationMs)
            {
                CloseToast();
            }
        }

        private void OnMouseEnterToast(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isPaused = true;
        }

        private void OnMouseLeaveToast(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isPaused = false;
        }

        private void OnDownloadMp4Click(object sender, RoutedEventArgs e)
        {
            TriggerDownload(isVideo: true);
        }

        private void OnDownloadMp3Click(object sender, RoutedEventArgs e)
        {
            TriggerDownload(isVideo: false);
        }

        private void TriggerDownload(bool isVideo)
        {
            _timer.Stop();
            _metadataCts?.Cancel();

            string title = !string.IsNullOrWhiteSpace(FetchedTitle) ? FetchedTitle : "Media Download";
            MainViewModel.Current?.QueueDirect(CurrentUrl, isVideo, title, FetchedThumbnailUrl);

            if (System.Windows.Application.Current is App app)
            {
                app.BringMainWindowToFront();
            }

            CloseToast();
        }

        private void OnOpenAppClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _timer.Stop();
            _metadataCts?.Cancel();

            if (System.Windows.Application.Current is App app)
            {
                app.BringMainWindowToFrontWithUrl(CurrentUrl);
            }

            CloseToast();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            CloseToast();
        }

        private void CloseToast()
        {
            _timer.Stop();
            _metadataCts?.Cancel();
            try
            {
                Close();
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            _metadataCts?.Cancel();
            _metadataCts?.Dispose();
            base.OnClosed(e);
        }
    }
}
