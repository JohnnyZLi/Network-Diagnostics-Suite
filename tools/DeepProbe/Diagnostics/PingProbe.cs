using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class PingProbe
{
    private static readonly byte[] Payload = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

    public static async Task<IPAddress> ResolveTargetAsync(string target, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(target, out var parsed)) return parsed;
        var addresses = await Dns.GetHostAddressesAsync(target, cancellationToken);
        return addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault()
            ?? throw new InvalidOperationException($"No address was returned for {target}.");
    }

    public static async Task<PingTargetReport> RunAsync(
        string label,
        IPAddress address,
        int count,
        bool exposeAddress,
        CancellationToken cancellationToken)
    {
        var samples = new List<double?>(count);
        using var ping = new Ping();
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(address, 1_500, Payload, new PingOptions(64, false))
                    .WaitAsync(cancellationToken);
                samples.Add(reply.Status == IPStatus.Success ? reply.RoundtripTime : null);
            }
            catch (PingException)
            {
                samples.Add(null);
            }
            if (index < count - 1) await Task.Delay(120, cancellationToken);
        }

        return new PingTargetReport(label, exposeAddress ? address.ToString() : null, Statistics.Summarize(samples));
    }
}
