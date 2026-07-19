using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class MtuProbe
{
    public static async Task<PathMtuReport> RunAsync(
        string target,
        IPAddress destination,
        CancellationToken cancellationToken)
    {
        if (destination.AddressFamily != AddressFamily.InterNetwork)
        {
            return new PathMtuReport(target, null, null, "IPv4 is required for this probe.");
        }

        var lowest = 512;
        var highest = 1472;
        int? best = null;
        using var ping = new Ping();
        while (lowest <= highest)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = lowest + (highest - lowest) / 2;
            var payload = new byte[candidate];
            try
            {
                var reply = await ping.SendPingAsync(destination, 1_500, payload, new PingOptions(64, true))
                    .WaitAsync(cancellationToken);
                if (reply.Status == IPStatus.Success)
                {
                    best = candidate;
                    lowest = candidate + 1;
                }
                else
                {
                    highest = candidate - 1;
                }
            }
            catch (PingException)
            {
                highest = candidate - 1;
            }
        }

        return best is null
            ? new PathMtuReport(target, null, null, "No non-fragmented echo reply was received.")
            : new PathMtuReport(target, best, best + 28, best == 1472 ? "At least 1500 bytes" : "Estimated from ICMP echo replies");
    }
}
