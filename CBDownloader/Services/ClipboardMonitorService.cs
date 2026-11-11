using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CBDownloader.Services
{
    public class ClipboardMonitorService
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetClipboardSequenceNumber();

        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private IntPtr _windowHandle;
        private HwndSource? _hwndSource;
        
        public event EventHandler<string>? ClipboardUrlCopied;
        private uint _lastSequenceNumber = 0;
        private DateTime _lastFiredTime = DateTime.MinValue;

        public void StartMonitoring(Window window)
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            var source = HwndSource.FromHwnd(hwnd);
            if (source != null)
            {
                _windowHandle = hwnd;
                _hwndSource = source;
                source.AddHook(HwndHook);
                AddClipboardFormatListener(_windowHandle);
            }
        }

        public void StopMonitoring()
        {
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(HwndHook);
                _hwndSource = null;
            }
            if (_windowHandle != IntPtr.Zero)
            {
                RemoveClipboardFormatListener(_windowHandle);
                _windowHandle = IntPtr.Zero;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                try
                {
                    uint currentSequence = GetClipboardSequenceNumber();
                    if (currentSequence == _lastSequenceNumber)
                        return IntPtr.Zero;
                    
                    _lastSequenceNumber = currentSequence;

                    if (System.Windows.Clipboard.ContainsText())
                    {
                        var text = System.Windows.Clipboard.GetText();
                        if (!string.IsNullOrWhiteSpace(text) && text.Length <= 2048 && CBDownloader.Utils.RegexHelper.IsValidSupportedUrl(text))
                        {
                            if ((DateTime.UtcNow - _lastFiredTime).TotalMilliseconds < 500)
                                return IntPtr.Zero;
                                
                            _lastFiredTime = DateTime.UtcNow;
                            ClipboardUrlCopied?.Invoke(this, text);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            return IntPtr.Zero;
        }
    }
}
