using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CBDownloader.Services
{
    public static class PackageHelper
    {
        public const string StartupTaskId = "CBDownloaderStartupTask";

        private const int ErrorInsufficientBuffer = 122;
        private const int AppModelErrorNoPackage = 15700;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);

        private static readonly Lazy<bool> CachedIsPackaged = new(DetectIsPackaged);

        public static bool IsPackaged => CachedIsPackaged.Value;

        private static bool DetectIsPackaged()
        {
            try
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
                    return false;

                uint length = 0;
                int result = GetCurrentPackageFullName(ref length, null);
                return result != AppModelErrorNoPackage && (result == ErrorInsufficientBuffer || result == 0);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsLaunchedFromStartupTask()
        {
#if MSIX_BUILD
            if (IsPackaged)
            {
                try
                {
                    return Windows.ApplicationModel.AppInstance.GetActivatedEventArgs()?.Kind ==
                           Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
                }
                catch
                {
                    return false;
                }
            }
#endif
            return false;
        }

        public static async Task<bool> IsStartupTaskEnabledAsync()
        {
#if MSIX_BUILD
            if (IsPackaged)
            {
                try
                {
                    var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
                    return task.State == Windows.ApplicationModel.StartupTaskState.Enabled ||
                           task.State == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
                }
                catch
                {
                    return false;
                }
            }
#endif
            return false;
        }

        public static async Task<(bool Enabled, string? StatusMessage)> SetStartupTaskEnabledAsync(bool enable)
        {
#if MSIX_BUILD
            if (IsPackaged)
            {
                try
                {
                    var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
                    if (enable)
                    {
                        var newState = await task.RequestEnableAsync();
                        return newState switch
                        {
                            Windows.ApplicationModel.StartupTaskState.Enabled or
                            Windows.ApplicationModel.StartupTaskState.EnabledByPolicy => (true, null),
                            Windows.ApplicationModel.StartupTaskState.DisabledByUser => (false, "Enable startup in Windows Settings > Apps > Startup."),
                            Windows.ApplicationModel.StartupTaskState.DisabledByPolicy => (false, "Startup is disabled by system policy."),
                            _ => (false, "Startup could not be enabled.")
                        };
                    }

                    task.Disable();
                    return (false, null);
                }
                catch
                {
                    return (false, "Startup could not be changed.");
                }
            }
#endif
            return (false, null);
        }
    }
}
