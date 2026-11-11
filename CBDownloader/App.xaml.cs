using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CBDownloader.Models;
using CBDownloader.Services;
using CBDownloader.ViewModels;
using CBDownloader.Views;
using Forms = System.Windows.Forms;

namespace CBDownloader
{
    public partial class App : System.Windows.Application
    {
        private ClipboardMonitorService? _clipboardMonitor;
        private MainWindow? _mainWindow;
        private MainViewModel? _mainViewModel;
        private Forms.NotifyIcon? _notifyIcon;
        private EventWaitHandle? _showEvent;
        private bool _isExiting;
        private readonly MediaDownloadService _mediaService = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            try 
            {
                using var existingEvent = EventWaitHandle.OpenExisting("CBDownloaderShowEvent");
                existingEvent.Set();
                Environment.Exit(0);
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "CBDownloaderShowEvent");
                Task.Run(() =>
                {
                    while (!_isExiting)
                    {
                        try
                        {
                            if (_showEvent.WaitOne())
                            {
                                if (_isExiting) break;
                                Dispatcher.InvokeAsync(BringMainWindowToFront);
                            }
                        }
                        catch
                        {
                            break;
                        }
                    }
                });
            }

            base.OnStartup(e);

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _mainViewModel = new MainViewModel(_mediaService);
            _mainWindow = new MainWindow
            {
                DataContext = _mainViewModel,
                Topmost = SettingsService.Current.AlwaysOnTop
            };

            ThemeManager.Initialize();

            SetupNotifyIcon();

            _clipboardMonitor = new ClipboardMonitorService();
            _clipboardMonitor.ClipboardUrlCopied += (s, url) => ShowClipboardToast(url);

            var hwnd = new System.Windows.Interop.WindowInteropHelper(_mainWindow).EnsureHandle();
            _clipboardMonitor.StartMonitoring(_mainWindow);
        }

        public void BringMainWindowToFront()
        {
            if (_mainWindow == null) return;

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Show();

            // Force foreground
            _mainWindow.Topmost = true;
            _mainWindow.Topmost = SettingsService.Current.AlwaysOnTop;
            _mainWindow.Activate();
        }

        public void BringMainWindowToFrontWithUrl(string url)
        {
            BringMainWindowToFront();
        }

        private DownloadToastWindow? _currentToast;
        public void ShowClipboardToast(string url)
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentToast != null)
                {
                    _currentToast.SetUrlAndFetch(url);
                    _currentToast.PositionInBottomRight();
                    _currentToast.Show();
                    return;
                }

                _currentToast = new DownloadToastWindow(_mediaService, url);
                _currentToast.Closed += (s, e) => _currentToast = null;
                _currentToast.Show();
            });
        }

        private void SetupNotifyIcon()
        {
            _notifyIcon = new Forms.NotifyIcon();
            
            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/icon.ico");
                var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    using (streamInfo.Stream)
                    {
                        _notifyIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
                    }
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }

            _notifyIcon.Visible = true;
            _notifyIcon.Text = "CBDownloader";
            _notifyIcon.DoubleClick += (s, args) => BringMainWindowToFront();

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Show", null, (s, args) => BringMainWindowToFront());
            contextMenu.Items.Add("Settings", null, (s, args) => ShowSettings());
            contextMenu.Items.Add("Exit", null, (s, args) =>
            {
                _notifyIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        internal void ShowNotification(string title, string message)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(3000, title, message, Forms.ToolTipIcon.Info);
            }
        }

        private SettingsWindow? _settingsWindow;
        internal void ShowSettings()
        {
            Dispatcher.Invoke(() => 
            {
                if (_settingsWindow != null)
                {
                    _settingsWindow.Activate();
                    return;
                }

                _settingsWindow = new SettingsWindow
                {
                    Owner = _mainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
                _settingsWindow.ShowDialog();
            });
        }

        internal void ShowPlaylistWindow(MediaDownloadService mediaService, string url, string playlistTitle, bool isVideo)
        {
            Dispatcher.Invoke(() =>
            {
                using var playlistCts = new CancellationTokenSource();

                var vm = new PlaylistViewModel
                {
                    IsVideo = isVideo,
                    PlaylistTitle = playlistTitle,
                    IsLoading = true
                };

                var window = new PlaylistWindow
                {
                    DataContext = vm,
                    Owner = _mainWindow,
                    Title = isVideo ? "Queue Playlist Videos" : "Queue Playlist Audios"
                };

                window.Closed += (s, e) => playlistCts.Cancel();
                vm.CloseAction = () => Dispatcher.Invoke(() => window.Close());

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await mediaService.GetPlaylistMetadataAsync(url, playlistCts.Token);
                        if (!playlistCts.Token.IsCancellationRequested)
                        {
                            Dispatcher.Invoke(() => vm.LoadEntries(result.PlaylistTitle, result.Items));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        if (!playlistCts.Token.IsCancellationRequested)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                vm.LoadingStatus = $"Error loading playlist: {ex.Message}";
                            });
                        }
                    }
                });

                window.ShowDialog();

                if (vm.DownloadRequested && _mainViewModel != null)
                {
                    var selected = vm.GetSelectedItems();
                    if (selected.Count > 0)
                        _mainViewModel.AddPlaylistItemsToQueue(selected, isVideo, vm.PlaylistTitle);
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _isExiting = true;
            try
            {
                _showEvent?.Set();
                _showEvent?.Dispose();
            }
            catch { }

            if (_currentToast != null)
            {
                try
                {
                    _currentToast.Close();
                }
                catch { }
                _currentToast = null;
            }

            _clipboardMonitor?.StopMonitoring();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnExit(e);
        }
    }
}
