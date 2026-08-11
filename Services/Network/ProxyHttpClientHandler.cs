using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MovieManagerDesktop.Services;

namespace MovieManagerDesktop.Services.Network
{
    public class RouteDecision
    {
        public bool UseProxy { get; set; }
        public string? SuccessfulWorkerUrl { get; set; }
        public long ExpirationTime { get; set; }
        public int ConsecutiveFailures { get; set; }
    }

    public class ProxyHttpClientHandler : DelegatingHandler
    {
        private static readonly long TTL_MS = 15 * 60 * 1000L; // 15 minutes
        private static readonly ConcurrentDictionary<string, RouteDecision> Cache = new();
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public static void ClearCache()
        {
            Cache.Clear();
            LoggerService.Info("[Network] Route cache cleared");
        }

        public ProxyHttpClientHandler() : this(new HttpClientHandler())
        {
        }

        public ProxyHttpClientHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        private bool IsNetworkException(Exception e)
        {
            return e is HttpRequestException || e is TaskCanceledException || e is System.Net.Sockets.SocketException || e is System.IO.IOException;
        }

        private bool IsBlockedResponse(HttpResponseMessage response)
        {
            if (response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Extracts a short, safe description of the URL for logging (no API keys).
        /// </summary>
        private string SafeUrl(Uri? uri)
        {
            if (uri == null) return "(null)";
            // Remove api_key / apikey params from query for safe logging
            string url = uri.GetLeftPart(UriPartial.Path);
            string query = uri.Query;
            if (!string.IsNullOrEmpty(query))
            {
                query = Regex.Replace(query, @"[?&]?api_key=[^&]+", "");
                query = Regex.Replace(query, @"[?&]?apikey=[^&]+", "");
                if (query.StartsWith("&")) query = "?" + query.Substring(1);
                url += query;
            }
            return url;
        }

        /// <summary>
        /// Returns a short label for the proxy URL for logging.
        /// </summary>
        private string ProxyLabel(string proxyUrl)
        {
            try
            {
                var uri = new Uri(proxyUrl);
                return uri.Host;
            }
            catch
            {
                return proxyUrl.Length > 40 ? proxyUrl.Substring(0, 40) + "..." : proxyUrl;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Skip logging/proxy logic for already-proxied requests
            if (request.Options.TryGetValue(new HttpRequestOptionsKey<bool>("Proxied"), out bool proxied) && proxied)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            var originalUri = request.RequestUri;
            if (originalUri == null) return await base.SendAsync(request, cancellationToken);

            string host = originalUri.Host;
            string urlString = originalUri.ToString();

            bool shouldProxy = urlString.Contains("themoviedb.org", StringComparison.OrdinalIgnoreCase) || 
                               urlString.Contains("tmdb.org", StringComparison.OrdinalIgnoreCase) || 
                               urlString.Contains("omdbapi.com", StringComparison.OrdinalIgnoreCase) ||
                               urlString.Contains("tvmaze.com", StringComparison.OrdinalIgnoreCase) ||
                               urlString.Contains("anilist.co", StringComparison.OrdinalIgnoreCase);

            if (!shouldProxy)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            RouteDecision decision;
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (Cache.TryGetValue(host, out var currentDecision) && currentDecision.ExpirationTime > now)
                {
                    decision = currentDecision;
                }
                else
                {
                    decision = new RouteDecision { UseProxy = false, ExpirationTime = now + TTL_MS };
                    Cache[host] = decision;
                }
            }
            finally
            {
                _semaphore.Release();
            }

            string safeUrl = SafeUrl(originalUri);

            if (!decision.UseProxy)
            {
                // ── Step 1: Try direct connection ──
                LoggerService.Info($"[Network] ➜ Direct request: {safeUrl}");
                var sw = Stopwatch.StartNew();
                try
                {
                    var response = await base.SendAsync(request, cancellationToken);
                    sw.Stop();

                    if (IsBlockedResponse(response))
                    {
                        LoggerService.Warning($"[Network] ✖ BLOCKED (403/HTML) from {host} — Status: {(int)response.StatusCode} — {sw.ElapsedMilliseconds}ms — Switching to proxy...");
                        response.Dispose();
                        RecordFailureAndFlip(host);
                        return await ExecuteWithProxyAsync(request, decision.SuccessfulWorkerUrl, cancellationToken);
                    }

                    LoggerService.Info($"[Network] ✔ Direct OK — {host} — Status: {(int)response.StatusCode} — {sw.ElapsedMilliseconds}ms");
                    return response;
                }
                catch (Exception e) when (IsNetworkException(e))
                {
                    sw.Stop();
                    string errorType = e.GetType().Name;
                    string errorMsg = e.InnerException?.Message ?? e.Message;
                    LoggerService.Warning($"[Network] ✖ Direct FAILED — {host} — {errorType}: {errorMsg} — {sw.ElapsedMilliseconds}ms — Switching to proxy...");
                    RecordFailureAndFlip(host);
                    return await ExecuteWithProxyAsync(request, decision.SuccessfulWorkerUrl, cancellationToken);
                }
            }
            else
            {
                // ── Route cache says: use proxy directly ──
                string cachedProxy = decision.SuccessfulWorkerUrl != null ? ProxyLabel(decision.SuccessfulWorkerUrl) : "auto";
                LoggerService.Info($"[Network] ➜ Cached route → proxy ({cachedProxy}) for {host}");
                return await ExecuteWithProxyAsync(request, decision.SuccessfulWorkerUrl, cancellationToken);
            }
        }

        private void RecordFailureAndFlip(string host)
        {
            if (Cache.TryGetValue(host, out var current))
            {
                int failures = current.ConsecutiveFailures + 1;
                if (failures >= 2)
                {
                    LoggerService.Info($"[Network] ⚑ Route flipped: {host} → PROXY (after {failures} consecutive failures, TTL=15min)");
                    Cache[host] = new RouteDecision
                    {
                        UseProxy = true,
                        SuccessfulWorkerUrl = current.SuccessfulWorkerUrl,
                        ExpirationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + TTL_MS,
                        ConsecutiveFailures = 0
                    };
                }
                else
                {
                    LoggerService.Info($"[Network] ⚠ Failure #{failures} for {host} (need 2 to flip to proxy)");
                    current.ConsecutiveFailures = failures;
                }
            }
            else
            {
                LoggerService.Info($"[Network] ⚑ Route flipped: {host} → PROXY (first failure, no prior cache)");
                Cache[host] = new RouteDecision
                {
                    UseProxy = true,
                    ExpirationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + TTL_MS,
                    ConsecutiveFailures = 0
                };
            }
        }

        private async Task<HttpResponseMessage> ExecuteWithProxyAsync(HttpRequestMessage request, string? knownGoodUrl, CancellationToken cancellationToken)
        {
            string originalUrlStr = request.RequestUri!.ToString();
            string safeOriginal = SafeUrl(request.RequestUri);
            
            var settings = SettingsManager.LoadSettings();
            List<string> proxyUrls = new();
            if (settings.IsApiProxyEnabled && !string.IsNullOrWhiteSpace(settings.ApiProxyUrl))
            {
                proxyUrls.AddRange(settings.ApiProxyUrl.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            }
            
            // Add defaults like Android
            foreach (var defaultUrl in SettingsManager.DefaultProxyUrls)
            {
                if (!proxyUrls.Contains(defaultUrl, StringComparer.OrdinalIgnoreCase))
                    proxyUrls.Add(defaultUrl);
            }

            var urlsToTry = new List<string>();
            if (!string.IsNullOrEmpty(knownGoodUrl) && proxyUrls.Contains(knownGoodUrl))
            {
                urlsToTry.Add(knownGoodUrl);
                urlsToTry.AddRange(proxyUrls.Where(u => u != knownGoodUrl));
            }
            else
            {
                urlsToTry = proxyUrls;
            }

            var distinctUrls = urlsToTry.Distinct().ToList();
            LoggerService.Info($"[Network] 🔄 Trying {distinctUrls.Count} proxy(s) for: {safeOriginal}");

            int proxyIndex = 0;
            foreach (var proxySetting in distinctUrls)
            {
                proxyIndex++;
                string trimmedProxy = proxySetting.Trim();
                if (string.IsNullOrWhiteSpace(trimmedProxy)) continue;

                string proxyLabel = ProxyLabel(trimmedProxy);

                string proxyUrlBase;
                if (trimmedProxy.Contains("?"))
                {
                    proxyUrlBase = trimmedProxy.EndsWith("url=") ? trimmedProxy : trimmedProxy + "&url=";
                }
                else
                {
                    proxyUrlBase = trimmedProxy.EndsWith("/") ? trimmedProxy + "?url=" : trimmedProxy + "/?url=";
                }

                string encodedOriginalUrl = Uri.EscapeDataString(originalUrlStr);
                string newUrl = proxyUrlBase + encodedOriginalUrl;

                string cleanUrl = Regex.Replace(newUrl, @"&?api_key=[^&]+", "");
                cleanUrl = Regex.Replace(cleanUrl, @"&?apikey=[^&]+", "");

                var newRequest = new HttpRequestMessage(request.Method, cleanUrl);
                foreach (var header in request.Headers) newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                if (request.Content != null)
                {
                    var stream = new System.IO.MemoryStream();
                    await request.Content.CopyToAsync(stream);
                    stream.Position = 0;
                    newRequest.Content = new StreamContent(stream);
                    foreach (var header in request.Content.Headers) newRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                
                newRequest.Options.Set(new HttpRequestOptionsKey<bool>("Proxied"), true);

                LoggerService.Info($"[Network]   Proxy [{proxyIndex}/{distinctUrls.Count}]: {proxyLabel}...");
                var sw = Stopwatch.StartNew();

                try
                {
                    var response = await base.SendAsync(newRequest, cancellationToken);
                    sw.Stop();

                    if (response.IsSuccessStatusCode && !IsBlockedResponse(response))
                    {
                        LoggerService.Info($"[Network]   ✔ Proxy OK — {proxyLabel} — Status: {(int)response.StatusCode} — {sw.ElapsedMilliseconds}ms");
                        Cache[request.RequestUri.Host] = new RouteDecision
                        {
                            UseProxy = true,
                            SuccessfulWorkerUrl = proxySetting,
                            ExpirationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + TTL_MS,
                            ConsecutiveFailures = 0
                        };
                        return response;
                    }
                    else
                    {
                        LoggerService.Warning($"[Network]   ✖ Proxy FAILED — {proxyLabel} — Status: {(int)response.StatusCode} — {sw.ElapsedMilliseconds}ms — trying next...");
                        response.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    string errorType = ex.GetType().Name;
                    string errorMsg = ex.InnerException?.Message ?? ex.Message;
                    LoggerService.Warning($"[Network]   ✖ Proxy ERROR — {proxyLabel} — {errorType}: {errorMsg} — {sw.ElapsedMilliseconds}ms — trying next...");
                }
            }

            LoggerService.Error($"[Network] ✖✖ ALL proxies failed for {request.RequestUri.Host} — falling back to direct request");
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
