using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        private static bool _hasShownWarningInSession = false;

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

        /// <summary>
        /// Detects if VPN or System Proxy / TUN interface is active on Windows.
        /// </summary>
        public static bool IsVpnActive()
        {
            try
            {
                // 1. Check system web proxy (used by Clash, v2rayN, Sing-box, etc.)
                var defaultProxy = System.Net.WebRequest.DefaultWebProxy;
                if (defaultProxy != null)
                {
                    var testUri = new Uri("https://api.themoviedb.org");
                    var proxyUri = defaultProxy.GetProxy(testUri);
                    if (proxyUri != null && proxyUri != testUri)
                    {
                        return true;
                    }
                }

                // 2. Check active network adapters for VPN, Tunnel, or TAP/TUN adapters
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Ppp ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        return true;
                    }

                    string desc = (ni.Description + " " + ni.Name).ToLowerInvariant();
                    if (desc.Contains("vpn") ||
                        desc.Contains("wireguard") ||
                        desc.Contains("wintun") ||
                        desc.Contains("tap") ||
                        desc.Contains("tun") ||
                        desc.Contains("wiresock") ||
                        desc.Contains("windscribe") ||
                        desc.Contains("tunnelbear") ||
                        desc.Contains("openvpn") ||
                        desc.Contains("clash") ||
                        desc.Contains("sing-box") ||
                        desc.Contains("v2ray") ||
                        desc.Contains("xray") ||
                        desc.Contains("tailscale") ||
                        desc.Contains("zerotier") ||
                        desc.Contains("proton") ||
                        desc.Contains("nord") ||
                        desc.Contains("express") ||
                        desc.Contains("hiddify") ||
                        desc.Contains("nekoray") ||
                        desc.Contains("shadowsocks") ||
                        desc.Contains("anyconnect") ||
                        desc.Contains("fortinet") ||
                        desc.Contains("ikev2"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore inspection exceptions
            }

            return false;
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

        private string SafeUrl(Uri? uri)
        {
            if (uri == null) return "(null)";
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
            // Skip proxy logic for already-proxied requests
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

            bool vpnActive = IsVpnActive();

            RouteDecision decision;
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (Cache.TryGetValue(host, out var currentDecision) && currentDecision.ExpirationTime > now && !vpnActive)
                {
                    decision = currentDecision;
                }
                else
                {
                    // If VPN is active or cache expired, always try direct connection first
                    decision = new RouteDecision { UseProxy = false, ExpirationTime = now + TTL_MS };
                    Cache[host] = decision;
                }
            }
            finally
            {
                _semaphore.Release();
            }

            string safeUrl = SafeUrl(originalUri);

            if (!decision.UseProxy || vpnActive)
            {
                // ── Step 1: Try direct connection with smart timeout ──
                LoggerService.Info($"[Network] ➜ Direct request {(vpnActive ? "[VPN Active]" : "")}: {safeUrl}");
                var sw = Stopwatch.StartNew();
                try
                {
                    // Clone request for direct attempt
                    var directRequest = await CloneHttpRequestAsync(request);

                    // 4-second timeout for direct check so user doesn't get stuck for 21 seconds if filtered
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(4));

                    var response = await base.SendAsync(directRequest, cts.Token);
                    sw.Stop();

                    if (IsBlockedResponse(response))
                    {
                        LoggerService.Warning($"[Network] ✖ BLOCKED (403/HTML) from {host} — Status: {(int)response.StatusCode} — {sw.ElapsedMilliseconds}ms — Switching to proxy...");
                        response.Dispose();
                        if (!vpnActive) RecordFailureAndFlip(host);
                        return await ExecuteWithProxyAsync(request, decision.SuccessfulWorkerUrl, cancellationToken);
                    }

                    // Direct connection succeeded (e.g. VPN active or clean connection)
                    LoggerService.Info($"[Network] ✔ Direct OK — {host} — Status: {(int)response.StatusCode} — {sw.ElapsedMilliseconds}ms");
                    Cache[host] = new RouteDecision { UseProxy = false, ExpirationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + TTL_MS };
                    return response;
                }
                catch (Exception e) when (IsNetworkException(e) || e is OperationCanceledException)
                {
                    sw.Stop();
                    string errorType = e.GetType().Name;
                    string errorMsg = e.InnerException?.Message ?? e.Message;
                    LoggerService.Warning($"[Network] ✖ Direct FAILED — {host} — {errorType}: {errorMsg} — {sw.ElapsedMilliseconds}ms — Switching to proxy...");
                    if (!vpnActive) RecordFailureAndFlip(host);
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

        private static async Task<HttpRequestMessage> CloneHttpRequestAsync(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri);
            foreach (var header in req.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (req.Content != null)
            {
                var ms = new System.IO.MemoryStream();
                await req.Content.CopyToAsync(ms);
                ms.Position = 0;
                clone.Content = new StreamContent(ms);
                foreach (var header in req.Content.Headers) clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            return clone;
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

            // If no proxies available, try syncing from GitHub dynamically
            if (proxyUrls.Count == 0)
            {
                LoggerService.Info("[Network] No proxies in settings. Attempting dynamic sync from GitHub...");
                await SettingsManager.SyncEncryptedProxiesAsync();
                settings = SettingsManager.LoadSettings();
                if (!string.IsNullOrWhiteSpace(settings.ApiProxyUrl))
                {
                    proxyUrls.AddRange(settings.ApiProxyUrl.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                }
            }

            if (proxyUrls.Count == 0)
            {
                LoggerService.Warning("[Network] No active proxies available on cloud or local. Falling back to direct request.");
                return await base.SendAsync(request, cancellationToken);
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

            async Task<HttpResponseMessage?> TryProxyListAsync(List<string> urls)
            {
                int proxyIndex = 0;
                foreach (var proxySetting in urls)
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

                    var newRequest = await CloneHttpRequestAsync(request);
                    newRequest.RequestUri = new Uri(newUrl);
                    newRequest.Options.Set(new HttpRequestOptionsKey<bool>("Proxied"), true);

                    LoggerService.Info($"[Network]   Proxy [{proxyIndex}/{urls.Count}]: {proxyLabel}...");
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
                return null;
            }

            var resultResponse = await TryProxyListAsync(distinctUrls);
            if (resultResponse != null) return resultResponse;

            // All local proxies failed -> Try dynamic sync from GitHub to fetch fresh proxies
            LoggerService.Info("[Network] All local proxies failed. Fetching fresh proxies from GitHub...");
            await SettingsManager.SyncEncryptedProxiesAsync();
            settings = SettingsManager.LoadSettings();
            var refreshedUrls = (settings.ApiProxyUrl ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Except(distinctUrls)
                .ToList();

            if (refreshedUrls.Count > 0)
            {
                resultResponse = await TryProxyListAsync(refreshedUrls);
                if (resultResponse != null) return resultResponse;
            }

            if (!_hasShownWarningInSession)
            {
                _hasShownWarningInSession = true;
                ToastService.Instance.ShowWarning("عدم دسترسی به سرورهای ضدتحریم؛ اتصال مستقیم در حال تلاش است...");
            }

            LoggerService.Error($"[Network] ✖✖ ALL proxies failed for {request.RequestUri.Host} — falling back to direct request");
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
