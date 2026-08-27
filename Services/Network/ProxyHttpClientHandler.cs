using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

    /// <summary>
    /// Advanced Anti-Censorship & Anti-Filter HTTP DelegatingHandler for Desktop.
    /// Incorporates DNS-over-HTTPS (DoH), Anti-DPI TCP Fragmentation, VPN Detection,
    /// and Smart Cloud Proxy Failover matching the Android engine.
    /// </summary>
    public class ProxyHttpClientHandler : DelegatingHandler
    {
        private static readonly long TTL_MS = 15 * 60 * 1000L; // 15 minutes
        private static readonly ConcurrentDictionary<string, RouteDecision> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private static bool _hasShownWarningInSession = false;

        public static void ClearCache()
        {
            Cache.Clear();
            LoggerService.Info("[Network] Anti-censorship route cache cleared");
        }

        public ProxyHttpClientHandler() : this(CreateAntiCensorshipSocketsHandler())
        {
        }

        public ProxyHttpClientHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        /// <summary>
        /// Creates a high-performance SocketsHttpHandler configured with DoH and Anti-DPI TLS fragmentation.
        /// </summary>
        public static SocketsHttpHandler CreateAntiCensorshipSocketsHandler()
        {
            return new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(8),
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                EnableMultipleHttp2Connections = true,
                ConnectCallback = async (context, cancellationToken) =>
                {
                    string host = context.DnsEndPoint.Host;
                    int port = context.DnsEndPoint.Port;

                    // 1. Resolve host using RealDoHDns (bypasses DNS poisoning)
                    var ips = await RealDoHDns.ResolveAsync(host, cancellationToken);
                    var ip = ips.Length > 0 ? ips[0] : (await Dns.GetHostAddressesAsync(host, cancellationToken))[0];

                    // 2. Open TCP socket
                    var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };

                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        cts.CancelAfter(TimeSpan.FromSeconds(6));
                        await socket.ConnectAsync(new IPEndPoint(ip, port), cts.Token);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }

                    // 3. Wrap network stream with Anti-DPI packet fragmenter
                    var networkStream = new NetworkStream(socket, ownsSocket: true);
                    return new AntiDpiStream(networkStream);
                }
            };
        }

        /// <summary>
        /// Accurately detects if a real VPN, WireGuard, TUN/TAP, or System Proxy is actively connected on Windows.
        /// Ignores disconnected virtual NICs, filter drivers, and packet schedulers.
        /// </summary>
        public static bool IsVpnActive(out string vpnDescription)
        {
            vpnDescription = string.Empty;
            try
            {
                // 1. Check Windows Registry for active System Proxy (used by Clash, v2rayN, Sing-box, Nekoray, etc.)
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
                    if (key != null)
                    {
                        var proxyEnable = key.GetValue("ProxyEnable");
                        if (proxyEnable is int pe && pe == 1)
                        {
                            var proxyServer = key.GetValue("ProxyServer") as string;
                            if (!string.IsNullOrWhiteSpace(proxyServer))
                            {
                                vpnDescription = $"System Proxy ({proxyServer})";
                                return true;
                            }
                        }
                    }
                }
                catch { }

                // 2. Check active network adapters that are UP and have a valid IP assignment
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    string name = ni.Name.ToLowerInvariant();
                    string desc = ni.Description.ToLowerInvariant();

                    // Exclude lightweight filter drivers, packet schedulers, loopback, virtual switches
                    if (desc.Contains("filter") || desc.Contains("scheduler") || desc.Contains("lightweight") ||
                        desc.Contains("hyper-v") || desc.Contains("loopback") || desc.Contains("miniport") ||
                        name.Contains("filter") || name.Contains("loopback") || name.Contains("vswitch") ||
                        desc.Contains("kernel debug") || desc.Contains("direct virtual adapter"))
                    {
                        continue;
                    }

                    // Check if this interface has at least one valid IPv4 address (not 127.0.0.1 or 169.254.x.x APIPA)
                    var ipProps = ni.GetIPProperties();
                    bool hasValidIp = false;
                    foreach (var unicast in ipProps.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            string ip = unicast.Address.ToString();
                            if (!ip.StartsWith("127.") && !ip.StartsWith("169.254."))
                            {
                                hasValidIp = true;
                                break;
                            }
                        }
                    }

                    if (!hasValidIp)
                        continue;

                    // If it is a PPP connection (IKEv2, SSTP, L2TP, PPTP) that is UP with a valid IP
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Ppp)
                    {
                        vpnDescription = $"{ni.Name} (PPP)";
                        return true;
                    }

                    // Check specific VPN adapter names / hardware descriptions
                    if (name.Contains("wireguard") || name.Contains("wintun") || name.Contains("windscribe") ||
                        name.Contains("tunnelbear") || name.Contains("openvpn") || name.Contains("clash") ||
                        name.Contains("sing-box") || name.Contains("v2ray") || name.Contains("xray") ||
                        name.Contains("tailscale") || name.Contains("zerotier") || name.Contains("proton") ||
                        name.Contains("nord") || name.Contains("expressvpn") || name.Contains("hiddify") ||
                        name.Contains("nekoray") || name.Contains("shadowsocks") || name.Contains("anyconnect") ||
                        name.Contains("fortinet") || name.Contains("vpn") ||
                        desc.Contains("wireguard") || desc.Contains("wintun") || desc.Contains("tap-windows") ||
                        desc.Contains("openvpn") || desc.Contains("windscribe") || desc.Contains("tunnelbear") ||
                        desc.Contains("wintun userspace") || desc.Contains("vpn adapter"))
                    {
                        vpnDescription = ni.Name;
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

        public static bool IsVpnActive() => IsVpnActive(out _);

        private static string GetCacheKey(string host)
        {
            if (host.Contains("themoviedb.org", StringComparison.OrdinalIgnoreCase) || host.Contains("tmdb.org", StringComparison.OrdinalIgnoreCase))
                return "tmdb";
            if (host.Contains("omdbapi.com", StringComparison.OrdinalIgnoreCase))
                return "omdb";
            if (host.Contains("tvmaze.com", StringComparison.OrdinalIgnoreCase))
                return "tvmaze";
            if (host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || host.Contains("ytimg.com", StringComparison.OrdinalIgnoreCase))
                return "youtube";
            if (host.Contains("deadline.com", StringComparison.OrdinalIgnoreCase) || host.Contains("collider.com", StringComparison.OrdinalIgnoreCase) || host.Contains("variety.com", StringComparison.OrdinalIgnoreCase) || host.Contains("boxofficemojo.com", StringComparison.OrdinalIgnoreCase))
                return "cinemanews";

            return host.ToLowerInvariant();
        }

        private static bool IsNetworkException(Exception e)
        {
            return e is HttpRequestException || e is TaskCanceledException || e is SocketException || e is IOException || e is WebException;
        }

        private static bool IsBlockedResponse(HttpResponseMessage response)
        {
            if (response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
            return false;
        }

        private static string SafeUrl(Uri? uri)
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

        private static string ProxyLabel(string proxyUrl)
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
            string cacheKey = GetCacheKey(host);

            bool shouldProxy = urlString.Contains("themoviedb.org", StringComparison.OrdinalIgnoreCase) || 
                               urlString.Contains("tmdb.org", StringComparison.OrdinalIgnoreCase) || 
                               urlString.Contains("omdbapi.com", StringComparison.OrdinalIgnoreCase) ||
                               urlString.Contains("tvmaze.com", StringComparison.OrdinalIgnoreCase) ||
                               urlString.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                               urlString.Contains("ytimg.com", StringComparison.OrdinalIgnoreCase) ||
                               urlString.Contains("deadline.com", StringComparison.OrdinalIgnoreCase) ||
                               urlString.Contains("collider.com", StringComparison.OrdinalIgnoreCase) ||
                               urlString.Contains("variety.com", StringComparison.OrdinalIgnoreCase) ||
                               urlString.Contains("boxofficemojo.com", StringComparison.OrdinalIgnoreCase);

            if (!shouldProxy)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            bool vpnActive = IsVpnActive(out string vpnName);
            bool isPermanentlyBlockedInIran = cacheKey == "youtube" || cacheKey == "cinemanews";

            RouteDecision decision;
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (Cache.TryGetValue(cacheKey, out var currentDecision) && currentDecision.ExpirationTime > now && !vpnActive)
                {
                    decision = currentDecision;
                }
                else
                {
                    bool initialUseProxy = isPermanentlyBlockedInIran && !vpnActive;
                    decision = new RouteDecision 
                    { 
                        UseProxy = initialUseProxy, 
                        ExpirationTime = now + TTL_MS 
                    };
                    Cache[cacheKey] = decision;
                }
            }
            finally
            {
                _semaphore.Release();
            }

            string safeUrl = SafeUrl(originalUri);

            if (!decision.UseProxy || vpnActive)
            {
                // ── Step 1: Direct request with DoH & Anti-DPI ──
                string vpnTag = vpnActive ? $" [VPN: {vpnName}]" : "";
                LoggerService.Info($"[Network] ➜ Direct DoH/Anti-DPI request{vpnTag}: {safeUrl}");
                var sw = Stopwatch.StartNew();
                try
                {
                    var directRequest = await CloneHttpRequestAsync(request);

                    // 4-second timeout for quick direct attempt
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(4));

                    var response = await base.SendAsync(directRequest, cts.Token);
                    sw.Stop();

                    if (!response.IsSuccessStatusCode || IsBlockedResponse(response))
                    {
                        LoggerService.Warning($"[Network] ✖ BLOCKED or HTTP ERROR from {host} — Status: {(int)response.StatusCode} — {sw.ElapsedMilliseconds}ms — Switching to proxy...");
                        response.Dispose();
                        if (!vpnActive) RecordFailureAndFlip(cacheKey);
                        return await ExecuteWithProxyAsync(request, decision.SuccessfulWorkerUrl, cacheKey, cancellationToken);
                    }

                    // Direct connection succeeded
                    LoggerService.Info($"[Network] ✔ Direct OK — {host} — Status: {(int)response.StatusCode} — {sw.ElapsedMilliseconds}ms");
                    Cache[cacheKey] = new RouteDecision 
                    { 
                        UseProxy = false, 
                        ExpirationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + TTL_MS 
                    };
                    return response;
                }
                catch (Exception e) when (IsNetworkException(e) || e is OperationCanceledException)
                {
                    sw.Stop();
                    string errorType = e.GetType().Name;
                    string errorMsg = e.InnerException?.Message ?? e.Message;
                    LoggerService.Warning($"[Network] ✖ Direct FAILED — {host} — {errorType}: {errorMsg} — {sw.ElapsedMilliseconds}ms — Switching to proxy...");
                    if (!vpnActive) RecordFailureAndFlip(cacheKey);
                    return await ExecuteWithProxyAsync(request, decision.SuccessfulWorkerUrl, cacheKey, cancellationToken);
                }
            }
            else
            {
                // ── Route cache says: use proxy directly ──
                string cachedProxy = decision.SuccessfulWorkerUrl != null ? ProxyLabel(decision.SuccessfulWorkerUrl) : "auto";
                LoggerService.Info($"[Network] ➜ Cached route → proxy ({cachedProxy}) for {host}");
                return await ExecuteWithProxyAsync(request, decision.SuccessfulWorkerUrl, cacheKey, cancellationToken);
            }
        }

        private static async Task<HttpRequestMessage> CloneHttpRequestAsync(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri);
            foreach (var header in req.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (req.Content != null)
            {
                var ms = new MemoryStream();
                await req.Content.CopyToAsync(ms);
                ms.Position = 0;
                clone.Content = new StreamContent(ms);
                foreach (var header in req.Content.Headers) clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            return clone;
        }

        private static void RecordFailureAndFlip(string cacheKey)
        {
            if (Cache.TryGetValue(cacheKey, out var current))
            {
                int failures = current.ConsecutiveFailures + 1;
                if (failures >= 2)
                {
                    LoggerService.Info($"[Network] ⚑ Route flipped: {cacheKey} → PROXY (after {failures} consecutive failures, TTL=15min)");
                    Cache[cacheKey] = new RouteDecision
                    {
                        UseProxy = true,
                        SuccessfulWorkerUrl = current.SuccessfulWorkerUrl,
                        ExpirationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + TTL_MS,
                        ConsecutiveFailures = 0
                    };
                }
                else
                {
                    LoggerService.Info($"[Network] ⚠ Failure #{failures} for {cacheKey} (need 2 to flip to proxy)");
                    current.ConsecutiveFailures = failures;
                }
            }
            else
            {
                LoggerService.Info($"[Network] ⚑ Route flipped: {cacheKey} → PROXY (first failure, no prior cache)");
                Cache[cacheKey] = new RouteDecision
                {
                    UseProxy = true,
                    ExpirationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + TTL_MS,
                    ConsecutiveFailures = 0
                };
            }
        }

        private async Task<HttpResponseMessage> ExecuteWithProxyAsync(HttpRequestMessage request, string? knownGoodUrl, string cacheKey, CancellationToken cancellationToken)
        {
            string originalUrlStr = request.RequestUri!.ToString();
            List<string> proxyUrls = SettingsManager.GetEffectiveProxies();

            // If no proxies available, try syncing from GitHub dynamically
            if (proxyUrls.Count == 0)
            {
                LoggerService.Info("[Network] No proxies in cache. Attempting dynamic 24h sync from cloud...");
                await SettingsManager.SyncEncryptedProxiesAsync(force: true);
                proxyUrls = SettingsManager.GetEffectiveProxies();
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
            LoggerService.Info($"[Network] 🔄 Trying {distinctUrls.Count} proxy(s) for: {SafeUrl(request.RequestUri)}");

            async Task<HttpResponseMessage?> TryProxyListAsync(List<string> urls)
            {
                int proxyIndex = 0;
                foreach (var proxySetting in urls)
                {
                    proxyIndex++;
                    string trimmedProxy = proxySetting.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedProxy)) continue;

                    // Ensure scheme exists (http:// or https://)
                    if (!trimmedProxy.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !trimmedProxy.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        trimmedProxy = "https://" + trimmedProxy;
                    }

                    string proxyLabel = ProxyLabel(trimmedProxy);

                    string proxyUrlBase;
                    if (trimmedProxy.Contains("?"))
                        proxyUrlBase = trimmedProxy.EndsWith("&") ? trimmedProxy : trimmedProxy + "&";
                    else
                        proxyUrlBase = trimmedProxy.EndsWith("/") ? trimmedProxy + "?url=" : trimmedProxy + "/?url=";

                    string finalProxyUrl;
                    if (proxyUrlBase.Contains("url="))
                    {
                        finalProxyUrl = proxyUrlBase.Substring(0, proxyUrlBase.IndexOf("url=") + 4) + Uri.EscapeDataString(originalUrlStr);
                    }
                    else
                    {
                        finalProxyUrl = proxyUrlBase + "url=" + Uri.EscapeDataString(originalUrlStr);
                    }

                    if (!Uri.TryCreate(finalProxyUrl, UriKind.Absolute, out var targetUri) || 
                        (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps))
                    {
                        LoggerService.Warning($"[Network]   ✖ Invalid proxy URL format: {finalProxyUrl}, skipping...");
                        continue;
                    }

                    HttpRequestMessage newRequest;
                    try
                    {
                        newRequest = new HttpRequestMessage(request.Method, targetUri);
                        foreach (var header in request.Headers)
                        {
                            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
                            newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }

                        if (request.Content != null)
                        {
                            var ms = new MemoryStream();
                            await request.Content.CopyToAsync(ms);
                            ms.Position = 0;
                            newRequest.Content = new StreamContent(ms);
                            foreach (var header in request.Content.Headers)
                            {
                                newRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            }
                        }

                        newRequest.Options.Set(new HttpRequestOptionsKey<bool>("Proxied"), true);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Warning($"[Network]   ✖ Failed to construct proxy request: {ex.Message}");
                        continue;
                    }

                    var sw = Stopwatch.StartNew();
                    try
                    {
                        var response = await base.SendAsync(newRequest, cancellationToken);
                        sw.Stop();

                        bool isValidApiError = response.StatusCode == HttpStatusCode.NotFound || 
                                               response.StatusCode == HttpStatusCode.Unauthorized ||
                                               response.StatusCode == HttpStatusCode.BadRequest;

                        if ((response.IsSuccessStatusCode || isValidApiError) && !IsBlockedResponse(response))
                        {
                            LoggerService.Info($"[Network]   ✔ Proxy OK (or API Error) — {proxyLabel} — Status: {(int)response.StatusCode} — {sw.ElapsedMilliseconds}ms");
                            Cache[cacheKey] = new RouteDecision
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

            // All local proxies failed -> Trigger immediate emergency re-sync from cloud (force=true)
            LoggerService.Info("[Network] All active proxies failed. Immediately re-syncing from cloud...");
            await SettingsManager.SyncEncryptedProxiesAsync(force: true);
            var refreshedUrls = SettingsManager.GetEffectiveProxies().Except(distinctUrls).ToList();

            if (refreshedUrls.Count > 0)
            {
                resultResponse = await TryProxyListAsync(refreshedUrls);
                if (resultResponse != null) return resultResponse;
            }

            if (!_hasShownWarningInSession)
            {
                _hasShownWarningInSession = true;
                ToastService.Instance.ShowWarning("عدم دسترسی به سرورهای ضدتحریم؛ تلاش از طریق اتصال مستقیم...");
            }

            LoggerService.Error($"[Network] ✖✖ ALL proxies failed for {request.RequestUri.Host} — falling back to direct request");
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
