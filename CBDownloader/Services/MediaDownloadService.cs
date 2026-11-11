using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using CBDownloader.Models;
using CBDownloader.Utils;
using Newtonsoft.Json.Linq;

namespace CBDownloader.Services
{
    public class MediaDownloadService
    {
        private const string CookieFileName = "cookies.txt";
        private const string LegacyCookieFileName = "youtube_cookies.txt";

        private static readonly HttpClient SharedHttpClient = CreateHttpClient();
        private readonly SemaphoreSlim _binaryInitLock = new(1, 1);
        private bool _binariesUpdatedInSession;

        private readonly string _binFolder;
        private readonly string _cookiesPath;
        private readonly string _denoPath;
        private readonly YoutubeDL _ytdl;

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CBDownloader");
            return client;
        }

        public MediaDownloadService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _binFolder = Path.Combine(appData, "CBDownloader", "bin");
            if (!Directory.Exists(_binFolder)) Directory.CreateDirectory(_binFolder);

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var localYtDlp = Path.Combine(baseDir, "yt-dlp.exe");
            var localFFmpeg = Path.Combine(baseDir, "ffmpeg.exe");
            var localDeno = Path.Combine(baseDir, "deno.exe");

            _cookiesPath = Path.Combine(_binFolder, CookieFileName);
            _denoPath = File.Exists(localDeno) ? localDeno : Path.Combine(_binFolder, "deno.exe");

            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (!path.Contains(_binFolder, StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("PATH", _binFolder + ";" + path);
            }

            _ytdl = new YoutubeDL
            {
                YoutubeDLPath = File.Exists(localYtDlp) ? localYtDlp : Path.Combine(_binFolder, "yt-dlp.exe"),
                FFmpegPath = File.Exists(localFFmpeg) ? localFFmpeg : Path.Combine(_binFolder, "ffmpeg.exe")
            };

            MigrateLegacyCookieFile(_binFolder);
        }

        public async Task EnsureBinariesExist()
        {
            if (_binariesUpdatedInSession &&
                File.Exists(_denoPath) &&
                File.Exists(_ytdl.YoutubeDLPath) &&
                File.Exists(_ytdl.FFmpegPath))
            {
                return;
            }

            await _binaryInitLock.WaitAsync();
            try
            {
                var downloadTasks = new List<Task>();

                if (!File.Exists(_denoPath))
                {
                    downloadTasks.Add(DownloadDenoAsync(_binFolder));
                }
                if (!File.Exists(_ytdl.YoutubeDLPath))
                {
                    downloadTasks.Add(YoutubeDLSharp.Utils.DownloadYtDlp(_binFolder));
                }
                if (!File.Exists(_ytdl.FFmpegPath))
                {
                    downloadTasks.Add(YoutubeDLSharp.Utils.DownloadFFmpeg(_binFolder));
                }

                if (downloadTasks.Count > 0)
                {
                    await Task.WhenAll(downloadTasks);
                }

                if (!_binariesUpdatedInSession)
                {
                    _binariesUpdatedInSession = true;
                    await TryUpdateYtDlpAsync();
                }
            }
            finally
            {
                _binaryInitLock.Release();
            }
        }

        private async Task DownloadDenoAsync(string binFolder)
        {
            if (File.Exists(_denoPath)) return;

            var zipPath = Path.Combine(binFolder, "deno.zip");
            try
            {
                const string url = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";
                using (var response = await SharedHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                ZipFile.ExtractToDirectory(zipPath, binFolder, true);
            }
            finally
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
        }

        private async Task TryUpdateYtDlpAsync()
        {
            try
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _ytdl.YoutubeDLPath,
                        Arguments = "-U",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();
            }
            catch { }
        }

        private static void MigrateLegacyCookieFile(string binFolder)
        {
            var legacyPath = Path.Combine(binFolder, LegacyCookieFileName);
            var newPath = Path.Combine(binFolder, CookieFileName);

            if (File.Exists(legacyPath) && !File.Exists(newPath))
            {
                File.Move(legacyPath, newPath);
            }
            else if (File.Exists(legacyPath) && File.Exists(newPath))
            {
                File.Delete(legacyPath);
            }
        }

        private void ApplyCookiesIfEnabled(OptionSet options)
        {
            if (SettingsService.Current.UseBrowserCookies && File.Exists(_cookiesPath))
            {
                options.AddCustomOption("--cookies", $"\"{_cookiesPath}\"");
            }
        }

        public async Task<(string? Title, string? ThumbnailUrl)> GetFastMetadataAsync(string url, CancellationToken ct = default)
        {
            url = RegexHelper.NormalizeUrl(url);
            if (string.IsNullOrWhiteSpace(url)) return (null, null);

            // 1. Fast oEmbed for YouTube, TikTok, Vimeo, SoundCloud (~50ms - 200ms)
            try
            {
                string? oEmbedUrl = null;
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var host = uri.Host.ToLowerInvariant();
                    if (host.Contains("youtube.com") || host.Contains("youtu.be"))
                    {
                        oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(url)}&format=json";
                    }
                    else if (host.Contains("tiktok.com"))
                    {
                        oEmbedUrl = $"https://www.tiktok.com/oembed?url={Uri.EscapeDataString(url)}";
                    }
                    else if (host.Contains("vimeo.com"))
                    {
                        oEmbedUrl = $"https://vimeo.com/api/oembed.json?url={Uri.EscapeDataString(url)}";
                    }
                    else if (host.Contains("soundcloud.com"))
                    {
                        oEmbedUrl = $"https://soundcloud.com/oembed?url={Uri.EscapeDataString(url)}&format=json";
                    }
                }

                if (oEmbedUrl != null)
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                    using var resp = await SharedHttpClient.GetAsync(oEmbedUrl, linkedCts.Token);
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync(linkedCts.Token);
                        var doc = JObject.Parse(json);
                        string? title = (string?)doc["title"];
                        string? thumbnail = (string?)doc["thumbnail_url"];
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            return (title, thumbnail);
                        }
                    }
                }
            }
            catch { }

            // 2. Fallback to yt-dlp metadata extraction
            try
            {
                var meta = await GetVideoMetadataAsync(url, ct);
                return (meta?.Title, meta?.Thumbnail);
            }
            catch
            {
                return (null, null);
            }
        }

        public async Task<YoutubeDLSharp.Metadata.VideoData> GetVideoMetadataAsync(string url, CancellationToken ct = default)
        {
            url = RegexHelper.NormalizeUrl(url);
            bool isPurePlaylist = RegexHelper.IsYoutubePurePlaylistUrl(url);

            var baseOptions = new OptionSet
            {
                NoPlaylist = !isPurePlaylist,
                YesPlaylist = isPurePlaylist
            };

            var res = await _ytdl.RunVideoDataFetch(url, flat: isPurePlaylist, overrideOptions: baseOptions, ct: ct);
            if (res.Success && res.Data != null)
            {
                EnsurePlaylistThumbnailFallback(res.Data);
                return res.Data;
            }

            if (SettingsService.Current.UseBrowserCookies && File.Exists(_cookiesPath))
            {
                var cookieOpts = new OptionSet
                {
                    NoPlaylist = !isPurePlaylist,
                    YesPlaylist = isPurePlaylist
                };
                ApplyCookiesIfEnabled(cookieOpts);

                res = await _ytdl.RunVideoDataFetch(url, flat: isPurePlaylist, overrideOptions: cookieOpts, ct: ct);
                if (res.Success && res.Data != null)
                {
                    EnsurePlaylistThumbnailFallback(res.Data);
                    return res.Data;
                }
            }

            throw new Exception($"Failed to fetch metadata: {string.Join("\n", res.ErrorOutput)}");
        }

        private static void EnsurePlaylistThumbnailFallback(YoutubeDLSharp.Metadata.VideoData data)
        {
            if (string.IsNullOrEmpty(data.Thumbnail) && data.Entries != null && data.Entries.Length > 0)
            {
                data.Thumbnail = data.Entries[0].Thumbnail;
            }
        }

        public async Task<(string PlaylistTitle, List<PlaylistItemModel> Items)> GetPlaylistMetadataAsync(string url, CancellationToken ct = default)
        {
            url = RegexHelper.NormalizeUrl(url);

            var options = new OptionSet { YesPlaylist = true };
            ApplyCookiesIfEnabled(options);

            var res = await _ytdl.RunVideoDataFetch(url, flat: true, overrideOptions: options, ct: ct);

            if (!res.Success)
                throw new Exception($"Failed to fetch playlist: {string.Join("\n", res.ErrorOutput)}");

            var entries = res.Data?.Entries;
            if (entries == null || entries.Length == 0)
                throw new Exception("Playlist is empty or could not be parsed.");

            var items = new List<PlaylistItemModel>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var videoUrl = !string.IsNullOrEmpty(entry.WebpageUrl)
                    ? entry.WebpageUrl
                    : $"https://www.youtube.com/watch?v={entry.ID}";

                long? durationSeconds = entry.Duration.HasValue ? (long?)Convert.ToInt64(entry.Duration.Value) : null;
                string duration = durationSeconds.HasValue
                    ? TimeSpan.FromSeconds(durationSeconds.Value).ToString(durationSeconds.Value >= 3600 ? @"h\:mm\:ss" : @"m\:ss")
                    : string.Empty;

                items.Add(new PlaylistItemModel
                {
                    Index = i + 1,
                    Title = entry.Title ?? $"Video {i + 1}",
                    VideoUrl = videoUrl,
                    ThumbnailUrl = entry.Thumbnail ?? string.Empty,
                    Duration = duration
                });
            }

            return (res.Data?.Title ?? "Playlist", items);
        }

        private OptionSet CreateDownloadOptions(bool isVideo, bool accelerate, string outputFolder, bool includeCookies)
        {
            var options = new OptionSet
            {
                NoPlaylist = true,
                Output = Path.Combine(outputFolder, "%(title)s.%(ext)s")
            };

            if (accelerate)
            {
                options.ConcurrentFragments = 4;
            }

            if (!isVideo)
            {
                options.ExtractAudio = true;
                options.AudioFormat = AudioConversionFormat.Mp3;
                options.AudioQuality = 0;
            }
            else
            {
                options.AddCustomOption("--recode-video", "mp4");
            }

            if (includeCookies)
            {
                ApplyCookiesIfEnabled(options);
            }

            return options;
        }

        public async Task<RunResult<string>> DownloadAsync(string url, bool isVideo, bool accelerate, IProgress<DownloadProgress> progress, CancellationToken ct, string? playlistName = null)
        {
            url = RegexHelper.NormalizeUrl(url);

            var baseFolder = SettingsService.Current.DownloadFolderPath;
            var subFolder = isVideo ? "Videos" : "Audios";
            var finalOutputFolder = Path.Combine(baseFolder, subFolder);

            if (!string.IsNullOrWhiteSpace(playlistName))
            {
                var safeName = string.Join("_", playlistName.Split(Path.GetInvalidFileNameChars())).Trim();
                if (!string.IsNullOrEmpty(safeName))
                {
                    finalOutputFolder = Path.Combine(finalOutputFolder, safeName);
                }
            }

            if (!Directory.Exists(finalOutputFolder))
            {
                Directory.CreateDirectory(finalOutputFolder);
            }

            _ytdl.OutputFolder = finalOutputFolder;

            var options = CreateDownloadOptions(isVideo, accelerate, finalOutputFolder, includeCookies: false);
            var result = await RunDownloadWithRetry(url, isVideo, options, progress, ct);

            if (!result.Success && SettingsService.Current.UseBrowserCookies && File.Exists(_cookiesPath))
            {
                var cookieOptions = CreateDownloadOptions(isVideo, accelerate, finalOutputFolder, includeCookies: true);
                result = await RunDownloadWithRetry(url, isVideo, cookieOptions, progress, ct);
            }

            return result;
        }

        private async Task<RunResult<string>> RunDownloadWithRetry(string url, bool isVideo, OptionSet options, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            if (isVideo)
            {
                return await _ytdl.RunVideoDownload(url, format: "bestvideo[vcodec^=avc1]+bestaudio[acodec^=mp4a]/bestvideo+bestaudio/best",
                    overrideOptions: options, progress: progress, ct: ct);
            }
            else
            {
                return await _ytdl.RunAudioDownload(url, AudioConversionFormat.Mp3, overrideOptions: options, progress: progress, ct: ct);
            }
        }
    }
}
