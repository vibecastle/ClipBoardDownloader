using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CBDownloader.Utils;

namespace CBDownloader.Services
{
    public class ExtensionServerService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly object _cookieLock = new();
        private HttpListener _listener = new();
        private CancellationTokenSource _cts = new();

        public event EventHandler<string>? DownloadRequested;

        public void Start()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://127.0.0.1:44321/api/download/");
                _listener.Start();

                Task.Run(() => ListenAsync(_cts.Token));
            }
            catch (Exception)
            {
                // Port already in use or listener failed to start
            }
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
            }
            catch { }
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    AddCorsHeaders(context.Response);

                    // Handle preflight requests (CORS) from the browser extension
                    if (context.Request.HttpMethod == "OPTIONS")
                    {
                        context.Response.StatusCode = 200;
                        context.Response.Close();
                        continue;
                    }

                    if (context.Request.HttpMethod == "POST")
                    {
                        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                        var body = await reader.ReadToEndAsync();

                        try
                        {
                            var payload = JsonSerializer.Deserialize<ExtensionPayload>(body, JsonOptions);
                            if (payload != null && !string.IsNullOrEmpty(payload.Url))
                            {
                                if (payload.Cookies != null && payload.Cookies.Length > 0)
                                {
                                    SaveCookies(payload.Cookies);
                                }

                                bool isBackgroundSync = context.Request.Url?.AbsolutePath.EndsWith("/sync", StringComparison.OrdinalIgnoreCase) == true;
                                if (!isBackgroundSync && RegexHelper.IsValidSupportedUrl(payload.Url))
                                {
                                    DownloadRequested?.Invoke(this, payload.Url);
                                }

                                context.Response.StatusCode = 200;
                                context.Response.ContentType = "application/json";
                                using var writer = new StreamWriter(context.Response.OutputStream);
                                await writer.WriteAsync("{\"status\":\"success\"}");
                            }
                            else
                            {
                                context.Response.StatusCode = 400; // Bad Request
                            }
                        }
                        catch (Exception)
                        {
                            context.Response.StatusCode = 500;
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = 405; // Method not allowed
                    }

                    context.Response.Close();
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
                }
                catch (ObjectDisposedException)
                {
                    // Listener disposed
                }
                catch (Exception)
                {
                    // Other errors
                }
            }
        }

        private static void AddCorsHeaders(HttpListenerResponse response)
        {
            response.Headers["Access-Control-Allow-Origin"] = "*";
            response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        }

        private void SaveCookies(ExtensionCookie[] cookies)
        {
            var binFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CBDownloader", "bin");
            if (!Directory.Exists(binFolder))
            {
                Directory.CreateDirectory(binFolder);
            }

            var txtPath = Path.Combine(binFolder, "cookies.txt");
            var tempPath = Path.Combine(binFolder, "cookies.txt.tmp");

            lock (_cookieLock)
            {
                var cookieMap = new Dictionary<string, string>(StringComparer.Ordinal);
                long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (File.Exists(txtPath))
                {
                    try
                    {
                        foreach (var line in File.ReadLines(txtPath))
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                                continue;

                            var parts = line.Split('\t');
                            if (parts.Length >= 7)
                            {
                                if (long.TryParse(parts[4], out long exp) && exp > 0 && exp < nowUnix)
                                    continue; // Skip expired cookies

                                string key = $"{parts[0]}\t{parts[2]}\t{parts[5]}";
                                cookieMap[key] = line;
                            }
                        }
                    }
                    catch { }
                }

                long defaultExpiry = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeSeconds();

                foreach (var cookie in cookies)
                {
                    if (string.IsNullOrWhiteSpace(cookie.Domain) || string.IsNullOrWhiteSpace(cookie.Name))
                        continue;

                    string domain = cookie.Domain.StartsWith('.') ? cookie.Domain : "." + cookie.Domain;
                    string flag = "TRUE";
                    string path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path;
                    string secure = cookie.Secure ? "TRUE" : "FALSE";
                    long expiry = cookie.ExpirationDate > 0 ? (long)cookie.ExpirationDate : defaultExpiry;

                    string key = $"{domain}\t{path}\t{cookie.Name}";
                    cookieMap[key] = $"{domain}\t{flag}\t{path}\t{secure}\t{expiry}\t{cookie.Name}\t{cookie.Value}";
                }

                var sb = new StringBuilder();
                sb.AppendLine("# Netscape HTTP Cookie File");
                sb.AppendLine("# This file was generated by CBDownloader Extension");
                sb.AppendLine();

                foreach (var entry in cookieMap.Values)
                {
                    sb.AppendLine(entry);
                }

                File.WriteAllText(tempPath, sb.ToString());
                File.Move(tempPath, txtPath, overwrite: true);
            }

            if (!SettingsService.Current.UseBrowserCookies)
            {
                SettingsService.Current.UseBrowserCookies = true;
                SettingsService.Save();
            }
        }

        public class ExtensionPayload
        {
            public string? Url { get; set; }
            public ExtensionCookie[]? Cookies { get; set; }
        }

        public class ExtensionCookie
        {
            public string Domain { get; set; } = string.Empty;
            public string? Path { get; set; }
            public bool Secure { get; set; }
            public double ExpirationDate { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }
    }
}
