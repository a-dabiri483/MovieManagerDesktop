using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services.Network
{
    /// <summary>
    /// High-performance DNS-over-HTTPS (DoH) resolver with Clean IP overrides.
    /// Completely bypasses local DNS poisoning and ISP censorship in restricted regions.
    /// </summary>
    public static class RealDoHDns
    {
        private static readonly long DEFAULT_TTL_MS = 15 * 60 * 1000L; // 15 minutes

        // Hardcoded known clean IPs for critical media APIs to bypass DNS poisoning completely
        private static readonly Dictionary<string, List<string>> HardcodedIps = new(StringComparer.OrdinalIgnoreCase)
        {
            { "api.themoviedb.org", new() { "108.157.4.28", "108.157.4.45", "108.157.4.116", "65.9.85.80", "65.9.85.99", "65.9.85.127", "65.9.85.51" } },
            { "image.tmdb.org", new() { "108.157.4.28", "108.157.4.45", "108.157.4.116", "65.9.85.80", "65.9.85.99", "65.9.85.127" } },
            { "www.themoviedb.org", new() { "108.157.4.28", "108.157.4.45", "108.157.4.116", "65.9.85.80" } },
            { "themoviedb.org", new() { "108.157.4.28", "108.157.4.45", "108.157.4.116", "65.9.85.80" } },
            { "www.omdbapi.com", new() { "172.67.142.146", "104.21.36.14" } },
            { "omdbapi.com", new() { "172.67.142.146", "104.21.36.14" } },
            { "api.tvmaze.com", new() { "104.26.8.140", "104.26.9.140", "172.67.75.147" } },
            { "tvmaze.com", new() { "104.26.8.140", "104.26.9.140", "172.67.75.147" } },
            { "img.youtube.com", new() { "142.250.185.110", "142.250.185.78", "142.250.184.206", "172.217.16.206" } },
            { "i.ytimg.com", new() { "142.250.185.110", "142.250.185.78", "142.250.184.206", "172.217.16.206" } },
            { "deadline.com", new() { "192.0.66.168", "192.0.66.169", "192.0.66.170" } },
            { "collider.com", new() { "104.18.2.19", "104.18.3.19" } },
            { "variety.com", new() { "192.0.66.168", "192.0.66.169" } },
            { "boxofficemojo.com", new() { "143.204.225.105", "143.204.225.26", "143.204.225.68" } },
            { "raw.githubusercontent.com", new() { "185.199.108.133", "185.199.109.133", "185.199.110.133", "185.199.111.133" } }
        };

        private class DnsCacheEntry
        {
            public IPAddress[] Addresses { get; set; } = Array.Empty<IPAddress>();
            public long ExpiresAt { get; set; }
        }

        private static readonly ConcurrentDictionary<string, DnsCacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);

        private static readonly HttpClient DohClient = new(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(2500)
        })
        {
            Timeout = TimeSpan.FromMilliseconds(3000)
        };

        // Highly reliable DoH Providers tested in Iran (MCI, Irancell, Rightel, Shatel, TCI)
        private static readonly (string Url, string NameParam, string TypeParam, string Accept)[] DohProviders =
        {
            ("https://dns.adguard-dns.com/dns-query", "name", "type", "application/dns-json"),
            ("https://doh.dns.sb/dns-query", "name", "type", "application/dns-json"),
            ("https://1.1.1.1/dns-query", "name", "type", "application/dns-json"),
            ("https://1.0.0.1/dns-query", "name", "type", "application/dns-json"),
            ("https://8.8.8.8/resolve", "name", "type", "application/json"),
            ("https://free.shecan.ir/dns-query", "name", "type", "application/dns-json")
        };

        public static async Task<IPAddress[]> ResolveAsync(string hostname, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                throw new ArgumentException("Hostname cannot be empty", nameof(hostname));

            // 1. Direct IP check
            if (IPAddress.TryParse(hostname, out var ip))
            {
                return new[] { ip };
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 2. Check Cache
            if (Cache.TryGetValue(hostname, out var entry) && entry.ExpiresAt > now)
            {
                return entry.Addresses;
            }

            // 3. Hardcoded Clean IP fallback if available
            if (HardcodedIps.TryGetValue(hostname, out var cleanIps) && cleanIps.Count > 0)
            {
                var resolved = new List<IPAddress>();
                foreach (var ipStr in cleanIps)
                {
                    if (IPAddress.TryParse(ipStr, out var parsed))
                        resolved.Add(parsed);
                }

                if (resolved.Count > 0)
                {
                    // Cache clean IPs for 30 minutes
                    Cache[hostname] = new DnsCacheEntry
                    {
                        Addresses = resolved.ToArray(),
                        ExpiresAt = now + (30 * 60 * 1000L)
                    };
                    return resolved.ToArray();
                }
            }

            // 4. Query DoH Providers in parallel/fast fallback
            var dohIps = await QueryDoHAsync(hostname, cancellationToken);
            if (dohIps.Length > 0)
            {
                Cache[hostname] = new DnsCacheEntry
                {
                    Addresses = dohIps,
                    ExpiresAt = now + DEFAULT_TTL_MS
                };
                return dohIps;
            }

            // 5. System DNS Fallback
            try
            {
                var systemIps = await Dns.GetHostAddressesAsync(hostname, cancellationToken);
                if (systemIps.Length > 0)
                {
                    Cache[hostname] = new DnsCacheEntry
                    {
                        Addresses = systemIps,
                        ExpiresAt = now + DEFAULT_TTL_MS
                    };
                    return systemIps;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Warning($"[DNS] System DNS lookup failed for {hostname}: {ex.Message}");
            }

            throw new WebException($"Unable to resolve host: {hostname}");
        }

        private static async Task<IPAddress[]> QueryDoHAsync(string hostname, CancellationToken cancellationToken)
        {
            foreach (var (providerUrl, nameParam, typeParam, acceptHeader) in DohProviders)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(2200));

                    string queryUrl = $"{providerUrl}?{nameParam}={Uri.EscapeDataString(hostname)}&{typeParam}=A";
                    using var req = new HttpRequestMessage(HttpMethod.Get, queryUrl);
                    req.Headers.TryAddWithoutValidation("Accept", acceptHeader);

                    var resp = await DohClient.SendAsync(req, cts.Token);
                    if (!resp.IsSuccessStatusCode) continue;

                    string json = await resp.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("Answer", out var answers) && answers.ValueKind == JsonValueKind.Array)
                    {
                        var ips = new List<IPAddress>();
                        foreach (var ans in answers.EnumerateArray())
                        {
                            if (ans.TryGetProperty("type", out var t) && t.GetInt32() == 1 && // Type A
                                ans.TryGetProperty("data", out var d))
                            {
                                string data = d.GetString() ?? "";
                                if (IPAddress.TryParse(data, out var parsedIp))
                                {
                                    ips.Add(parsedIp);
                                }
                            }
                        }

                        if (ips.Count > 0)
                        {
                            return ips.ToArray();
                        }
                    }
                }
                catch
                {
                    // Try next DoH provider
                }
            }

            return Array.Empty<IPAddress>();
        }
    }
}
