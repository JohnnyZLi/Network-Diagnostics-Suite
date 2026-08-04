using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal sealed record CapturedNetworkState(
    NetworkStateSnapshot Report,
    string? GatewayAddress,
    string Fingerprint);

internal static class NetworkStateProbe
{
    public static CapturedNetworkState Capture(Uri origin, string? selectedInterfaceId, bool includeLocalIdentifiers)
    {
        var active = ActiveInterfaces();
        var selected = active.FirstOrDefault(item => string.Equals(item.Id, selectedInterfaceId, StringComparison.Ordinal))
            ?? active.FirstOrDefault(HasGateway)
            ?? active.FirstOrDefault();
        string? gateway = null;
        var families = new HashSet<string>(StringComparer.Ordinal);
        if (selected is not null)
        {
            try
            {
                var properties = selected.GetIPProperties();
                gateway = properties.GatewayAddresses
                    .Select(item => item.Address)
                    .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !address.Equals(IPAddress.Any))
                    ?.ToString();
                foreach (var address in properties.UnicastAddresses.Select(item => item.Address))
                {
                    families.Add(address.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6");
                }
            }
            catch (NetworkInformationException)
            {
                // A disappearing interface is represented by the after-snapshot comparison.
            }
            catch (PlatformNotSupportedException)
            {
                // The snapshot remains useful without address details.
            }
        }

        var tunnels = active
            .Where(IsTunnel)
            .Select((item, index) => includeLocalIdentifiers ? item.Name : $"Tunnel interface {index + 1}")
            .ToArray();
        var proxy = ProxyFor(origin);
        var report = new NetworkStateSnapshot(
            selected is null ? null : includeLocalIdentifiers ? selected.Id : "active-interface",
            selected is null ? null : includeLocalIdentifiers ? selected.Name : "Active interface",
            includeLocalIdentifiers ? gateway : null,
            families.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            proxy is null ? null : includeLocalIdentifiers ? proxy : "Configured system proxy",
            tunnels);
        var fingerprint = string.Join('|',
            selected?.Id ?? string.Empty,
            gateway ?? string.Empty,
            string.Join(',', families.OrderBy(item => item, StringComparer.Ordinal)),
            proxy ?? string.Empty,
            string.Join(',', active.Select(item => item.Id).OrderBy(item => item, StringComparer.Ordinal)));
        return new CapturedNetworkState(report, gateway, fingerprint);
    }

    public static NetworkChangeReport Compare(CapturedNetworkState before, CapturedNetworkState after, bool captivePortal)
    {
        var changes = new List<string>();
        if (!string.Equals(before.Report.InterfaceId, after.Report.InterfaceId, StringComparison.Ordinal)
            || InterfaceFingerprint(before) != InterfaceFingerprint(after))
        {
            changes.Add("The active interface changed during the run.");
        }
        if (!string.Equals(before.GatewayAddress, after.GatewayAddress, StringComparison.Ordinal))
        {
            changes.Add("The default gateway changed during the run.");
        }
        if (!before.Report.AddressFamilies.SequenceEqual(after.Report.AddressFamilies, StringComparer.Ordinal))
        {
            changes.Add("The active IP address families changed during the run.");
        }
        if (!string.Equals(before.Report.Proxy, after.Report.Proxy, StringComparison.Ordinal)
            || ProxyFingerprint(before) != ProxyFingerprint(after))
        {
            changes.Add("The effective system proxy changed during the run.");
        }
        if (!string.Equals(before.Fingerprint, after.Fingerprint, StringComparison.Ordinal) && changes.Count == 0)
        {
            changes.Add("The active network configuration changed during the run.");
        }
        return new NetworkChangeReport(before.Report, after.Report, changes.Count > 0, changes, captivePortal);
    }

    public static async Task<bool> CheckCaptivePortalAsync(Uri origin, CancellationToken cancellationToken)
    {
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, $"/api/ping?portal={Guid.NewGuid():N}"));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var code = (int)response.StatusCode;
            if (code is >= 300 and < 400) return true;
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            return mediaType is not null && mediaType.Contains("html", StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static NetworkInterface[] ActiveInterfaces()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up && item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToArray();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
        catch (PlatformNotSupportedException)
        {
            return [];
        }
    }

    private static bool HasGateway(NetworkInterface networkInterface)
    {
        try
        {
            return networkInterface.GetIPProperties().GatewayAddresses.Any(item => !item.Address.Equals(IPAddress.Any));
        }
        catch (NetworkInformationException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static bool IsTunnel(NetworkInterface networkInterface)
    {
        if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel) return true;
        var value = $"{networkInterface.Name} {networkInterface.Description}";
        return value.Contains("vpn", StringComparison.OrdinalIgnoreCase)
            || value.Contains("wireguard", StringComparison.OrdinalIgnoreCase)
            || value.Contains("utun", StringComparison.OrdinalIgnoreCase)
            || value.Contains("tunnel", StringComparison.OrdinalIgnoreCase)
            || value.Contains("tap", StringComparison.OrdinalIgnoreCase)
            || value.Contains("tun", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ProxyFor(Uri origin)
    {
        try
        {
            var proxy = HttpClient.DefaultProxy.GetProxy(origin);
            if (proxy is null || SameAuthority(proxy, origin)) return null;
            return proxy.IsDefaultPort ? $"{proxy.Scheme}://{proxy.Host}" : $"{proxy.Scheme}://{proxy.Host}:{proxy.Port}";
        }
        catch
        {
            return null;
        }
    }

    private static string InterfaceFingerprint(CapturedNetworkState state) =>
        state.Fingerprint.Split('|').ElementAtOrDefault(0) ?? string.Empty;

    private static string ProxyFingerprint(CapturedNetworkState state) =>
        state.Fingerprint.Split('|').ElementAtOrDefault(3) ?? string.Empty;

    private static bool SameAuthority(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;
}
