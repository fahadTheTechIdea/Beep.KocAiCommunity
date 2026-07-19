using System.Net;
using System.Net.Sockets;

namespace Beep.KocAiCommunity.Infrastructure.Datasets;

/// <summary>Reason a URL import was blocked, or <see cref="None"/> when it's allowed.</summary>
public enum UrlBlockReason { None, BadScheme, BadHost, PrivateAddress }

/// <summary>
/// SSRF guard for URL imports: only http/https, and the host must not resolve to a loopback, private,
/// link-local, or otherwise non-public address. Blocks the classic metadata endpoint and RFC1918 ranges.
/// </summary>
public static class UrlImportGuard
{
    public static UrlBlockReason Check(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return UrlBlockReason.BadScheme;
        }

        // A literal IP host is checked directly; a name is resolved and every address checked.
        var addresses = new List<IPAddress>();
        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            addresses.Add(literal);
        }
        else
        {
            try
            {
                addresses.AddRange(Dns.GetHostAddresses(uri.Host));
            }
            catch (SocketException)
            {
                return UrlBlockReason.BadHost;
            }

            if (addresses.Count == 0)
            {
                return UrlBlockReason.BadHost;
            }
        }

        return addresses.Any(IsPrivate) ? UrlBlockReason.PrivateAddress : UrlBlockReason.None;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10                                   // 10.0.0.0/8
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)    // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168)                 // 192.168.0.0/16
                || (b[0] == 169 && b[1] == 254)                 // 169.254.0.0/16 (link-local, incl. metadata)
                || b[0] == 0;                                   // 0.0.0.0/8
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;
        }

        return true; // unknown family — fail closed
    }
}
