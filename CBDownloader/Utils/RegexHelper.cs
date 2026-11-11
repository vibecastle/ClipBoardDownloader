using System;
using System.Text.RegularExpressions;

namespace CBDownloader.Utils
{
    public static class RegexHelper
    {
        private static readonly Regex YoutubePlaylistUrlRegex = new Regex(
            @"(?:https?:\/\/)?(?:www\.)?youtube\.com\/.*[?&]list=([^&\s]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex YoutubePurePlaylistUrlRegex = new Regex(
            @"(?:https?:\/\/)?(?:www\.)?youtube\.com\/playlist\?.*list=([^&\s]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex YoutubeShortsRegex = new Regex(
            @"(https?:\/\/(?:www\.)?youtube\.com)\/shorts\/([a-zA-Z0-9_-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static bool IsDomain(string host, string domain)
        {
            return host == domain || host.EndsWith("." + domain);
        }

        public static bool IsValidSupportedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (!Uri.TryCreate(EnsureProtocol(url.Trim()), UriKind.Absolute, out var uri))
                return false;

            if (uri.Scheme != "http" && uri.Scheme != "https")
                return false;

            var host = uri.Host.ToLowerInvariant();
            var path = uri.AbsolutePath.ToLowerInvariant();
            var query = uri.Query.ToLowerInvariant();

            // youtube.com / youtu.be
            if (IsDomain(host, "youtube.com"))
            {
                return path.Contains("/watch") || path.Contains("/shorts/") || path.Contains("/v/") || 
                       path.Contains("/embed/") || path.Contains("/playlist") || query.Contains("v=") || query.Contains("list=");
            }
            if (IsDomain(host, "youtu.be"))
            {
                return path.Length > 1;
            }

            // instagram.com
            if (IsDomain(host, "instagram.com"))
            {
                var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    var seg = segments[i];
                    if (seg == "p" || seg == "reel" || seg == "reels" || seg == "tv")
                    {
                        return true;
                    }
                }
                return false;
            }

            // tiktok.com
            if (IsDomain(host, "tiktok.com"))
            {
                if (host.StartsWith("vm.") || host.Contains(".vm.") || host.StartsWith("vt.") || host.Contains(".vt."))
                {
                    return true;
                }

                var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    var seg = segments[i];
                    if (seg == "video" || seg == "v" || seg == "t")
                    {
                        return true;
                    }
                }
                return false;
            }

            // twitter.com / x.com
            if (IsDomain(host, "twitter.com") || IsDomain(host, "x.com"))
            {
                return path.Contains("/status/");
            }

            // facebook.com / fb.watch
            if (IsDomain(host, "facebook.com") || IsDomain(host, "fb.watch"))
            {
                if (host.Contains("fb.watch")) return true;
                return path.Contains("/videos/") || path.Contains("/watch") || path.Contains("/reel/") || path.Contains("/posts/");
            }

            // reddit.com
            if (IsDomain(host, "reddit.com"))
            {
                return path.Contains("/comments/");
            }

            // twitch.tv
            if (IsDomain(host, "twitch.tv"))
            {
                return path.Contains("/videos/") || path.Contains("/clip/") || path.Contains("/clips/") || host.StartsWith("clips.") || host.Contains(".clips.");
            }

            // dailymotion.com
            if (IsDomain(host, "dailymotion.com"))
            {
                return path.Contains("/video/");
            }

            // soundcloud.com / mixcloud.com
            if (IsDomain(host, "soundcloud.com") || IsDomain(host, "mixcloud.com"))
            {
                var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                return segments.Length >= 2;
            }

            // bilibili.com
            if (IsDomain(host, "bilibili.com"))
            {
                return path.Contains("/video/");
            }

            // nicovideo.jp
            if (IsDomain(host, "nicovideo.jp"))
            {
                return path.Contains("/watch/");
            }

            // rumble.com
            if (IsDomain(host, "rumble.com"))
            {
                return path.StartsWith("/v") && path.Length > 2;
            }

            // bandcamp.com
            if (IsDomain(host, "bandcamp.com"))
            {
                return path.Contains("/track/") || path.Contains("/album/");
            }

            // streamable.com
            if (IsDomain(host, "streamable.com"))
            {
                var p = path.Trim('/');
                return !string.IsNullOrEmpty(p) && p != "home" && p != "login" && p != "signup" && p != "pricing" && p != "explore";
            }

            // bitchute.com
            if (IsDomain(host, "bitchute.com"))
            {
                return path.Contains("/video/");
            }

            // pinterest.com
            if (IsDomain(host, "pinterest.com"))
            {
                return path.Contains("/pin/");
            }

            // tumblr.com
            if (IsDomain(host, "tumblr.com"))
            {
                return path.Contains("/post/");
            }

            // linkedin.com
            if (IsDomain(host, "linkedin.com"))
            {
                return path.Contains("/posts/") || path.Contains("/learning/") || path.Contains("/video/");
            }

            // ted.com
            if (IsDomain(host, "ted.com"))
            {
                return path.Contains("/talks/");
            }

            // vk.com
            if (IsDomain(host, "vk.com"))
            {
                return path.Contains("/video") || path.Contains("/clip");
            }

            // ok.ru
            if (IsDomain(host, "ok.ru"))
            {
                return path.Contains("/video/");
            }

            // vimeo.com
            if (IsDomain(host, "vimeo.com"))
            {
                var p = path.Trim('/');
                return !string.IsNullOrEmpty(p) && (long.TryParse(p, out _) || p.Contains("showcase") || p.Contains("channels") || p.Contains("groups") || p.Contains("event"));
            }

            // odysee.com
            if (IsDomain(host, "odysee.com"))
            {
                return path.Length > 1;
            }

            return false;
        }

        public static bool IsYoutubePlaylistUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return YoutubePlaylistUrlRegex.IsMatch(url);
        }

        public static bool IsYoutubePurePlaylistUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return YoutubePurePlaylistUrlRegex.IsMatch(url);
        }

        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            url = EnsureProtocol(url.Trim());
            url = NormalizeYoutubeShorts(url);
            return url;
        }

        public static string EnsureProtocol(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + url;
            }
            return url;
        }

        private static string NormalizeYoutubeShorts(string url)
        {
            return YoutubeShortsRegex.Replace(url, "$1/v/$2");
        }
    }
}
