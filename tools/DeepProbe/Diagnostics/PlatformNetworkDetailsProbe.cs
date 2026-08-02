using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using NetworkDeepProbe.Models;

namespace NetworkDeepProbe.Diagnostics;

internal static class PlatformNetworkDetailsProbe
{
    public static async Task<PlatformNetworkDetailsReport> RunAsync(
        bool includeSensitive,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            var wifi = await RunCommandAsync("netsh", ["wlan", "show", "interfaces"], cancellationToken);
            var routes = await RunCommandAsync("route", ["print", "-4"], cancellationToken);
            return new PlatformNetworkDetailsReport(
                wifi.Success ? ParseWindowsWifi(wifi.Output, includeSensitive) : UnavailableWifi(wifi.Error),
                routes.Success ? ParseWindowsRoutes(routes.Output, includeSensitive) : UnavailableRoutes(routes.Error));
        }

        if (OperatingSystem.IsMacOS())
        {
            const string airport = "/System/Library/PrivateFrameworks/Apple80211.framework/Versions/Current/Resources/airport";
            var wifi = await RunCommandAsync(airport, ["-I"], cancellationToken);
            var routes = await RunCommandAsync("netstat", ["-rn", "-f", "inet"], cancellationToken);
            return new PlatformNetworkDetailsReport(
                wifi.Success ? ParseMacWifi(wifi.Output, includeSensitive) : UnavailableWifi(wifi.Error),
                routes.Success ? ParseUnixRoutes(routes.Output, includeSensitive, "ipv4") : UnavailableRoutes(routes.Error));
        }

        if (OperatingSystem.IsLinux())
        {
            var devices = await RunCommandAsync("iw", ["dev"], cancellationToken);
            WifiDetailsReport wifi;
            if (!devices.Success)
            {
                wifi = UnavailableWifi(devices.Error);
            }
            else
            {
                var interfaceName = ParseLinuxWifiInterface(devices.Output);
                if (interfaceName is null)
                {
                    wifi = new WifiDetailsReport("not-connected", null, null, null, null, null, null, null, null, null, null, "No wireless interface was reported by iw.");
                }
                else
                {
                    var link = await RunCommandAsync("iw", ["dev", interfaceName, "link"], cancellationToken);
                    wifi = link.Success
                        ? ParseLinuxWifi(link.Output, interfaceName, includeSensitive)
                        : UnavailableWifi(link.Error) with { InterfaceName = interfaceName };
                }
            }

            var routes = await RunCommandAsync("ip", ["route", "show"], cancellationToken);
            return new PlatformNetworkDetailsReport(
                wifi,
                routes.Success ? ParseLinuxRoutes(routes.Output, includeSensitive) : UnavailableRoutes(routes.Error));
        }

        return new PlatformNetworkDetailsReport(
            UnavailableWifi("This operating system does not have a platform provider."),
            UnavailableRoutes("This operating system does not have a platform provider."));
    }

    internal static WifiDetailsReport ParseWindowsWifi(string output, bool includeSensitive)
    {
        var values = ParseKeyValueLines(output);
        var state = Get(values, "State");
        if (!string.Equals(state, "connected", StringComparison.OrdinalIgnoreCase))
        {
            return new WifiDetailsReport("not-connected", Get(values, "Name"), null, null, null, null, null, Get(values, "Radio type"), null, null, Get(values, "Authentication"), null);
        }

        return new WifiDetailsReport(
            "available",
            Get(values, "Name"),
            includeSensitive ? Get(values, "SSID") : null,
            ParsePercent(Get(values, "Signal")),
            null,
            ParseInteger(Get(values, "Channel")),
            BandFromChannel(ParseInteger(Get(values, "Channel"))),
            Get(values, "Radio type"),
            ParseLong(Get(values, "Receive rate (Mbps)")),
            ParseLong(Get(values, "Transmit rate (Mbps)")),
            Get(values, "Authentication"),
            includeSensitive ? null : "SSID is hidden unless local identifiers are included.");
    }

    internal static WifiDetailsReport ParseMacWifi(string output, bool includeSensitive)
    {
        var values = ParseKeyValueLines(output);
        var channelText = Get(values, "channel");
        var channel = ParseInteger(channelText?.Split(',')[0]);
        var rssi = ParseInteger(Get(values, "agrCtlRSSI"));
        var signal = rssi is null ? null : Math.Clamp((rssi.Value + 100) * 2, 0, 100);
        var ssid = Get(values, "SSID");
        var status = string.IsNullOrWhiteSpace(ssid) ? "not-connected" : "available";

        return new WifiDetailsReport(
            status,
            Get(values, "interface") ?? "Wi-Fi",
            includeSensitive ? ssid : null,
            signal,
            rssi,
            channel,
            BandFromChannel(channel),
            Get(values, "lastTxRate") is null ? null : "802.11",
            null,
            ParseLong(Get(values, "lastTxRate")),
            Get(values, "link auth"),
            includeSensitive ? null : "SSID is hidden unless local identifiers are included.");
    }

    internal static string? ParseLinuxWifiInterface(string output)
    {
        foreach (var raw in Lines(output))
        {
            var line = raw.Trim();
            if (line.StartsWith("Interface ", StringComparison.Ordinal))
            {
                var value = line["Interface ".Length..].Trim();
                if (value.Length > 0) return value;
            }
        }
        return null;
    }

    internal static WifiDetailsReport ParseLinuxWifi(string output, string interfaceName, bool includeSensitive)
    {
        string? ssid = null;
        int? rssi = null;
        int? signal = null;
        int? channel = null;
        long? receive = null;
        long? transmit = null;

        foreach (var raw in Lines(output))
        {
            var line = raw.Trim();
            if (line.StartsWith("SSID:", StringComparison.OrdinalIgnoreCase)) ssid = line[5..].Trim();
            if (line.StartsWith("signal:", StringComparison.OrdinalIgnoreCase))
            {
                rssi = ParseInteger(line[7..].Trim().Split(' ')[0]);
                if (rssi is not null) signal = Math.Clamp((rssi.Value + 100) * 2, 0, 100);
            }
            if (line.StartsWith("freq:", StringComparison.OrdinalIgnoreCase))
            {
                var frequency = ParseInteger(line[5..].Trim());
                channel = ChannelFromFrequency(frequency);
            }
            if (line.StartsWith("rx bitrate:", StringComparison.OrdinalIgnoreCase)) receive = ParseRate(line[11..]);
            if (line.StartsWith("tx bitrate:", StringComparison.OrdinalIgnoreCase)) transmit = ParseRate(line[11..]);
        }

        return new WifiDetailsReport(
            string.IsNullOrWhiteSpace(ssid) ? "not-connected" : "available",
            interfaceName,
            includeSensitive ? ssid : null,
            signal,
            rssi,
            channel,
            BandFromChannel(channel),
            null,
            receive,
            transmit,
            null,
            includeSensitive ? null : "SSID is hidden unless local identifiers are included.");
    }

    internal static RoutingDetailsReport ParseWindowsRoutes(string output, bool includeSensitive)
    {
        var entries = new List<RouteEntryReport>();
        foreach (var raw in Lines(output))
        {
            var parts = SplitColumns(raw);
            if (parts.Length != 5
                || !IPAddress.TryParse(parts[0], out _)
                || !IPAddress.TryParse(parts[1], out _)) continue;
            var prefix = PrefixLength(parts[1]);
            entries.Add(new RouteEntryReport(
                $"{parts[0]}/{prefix}",
                includeSensitive ? parts[2] : null,
                includeSensitive ? parts[3] : null,
                ParseInteger(parts[4]),
                "ipv4",
                parts[0] == "0.0.0.0" && prefix == 0));
        }
        return AvailableRoutes(entries);
    }

    internal static RoutingDetailsReport ParseLinuxRoutes(string output, bool includeSensitive)
    {
        var entries = new List<RouteEntryReport>();
        foreach (var raw in Lines(output))
        {
            var parts = SplitColumns(raw);
            if (parts.Length == 0) continue;
            var isDefault = parts[0] == "default";
            var destination = isDefault ? "0.0.0.0/0" : parts[0];
            var via = ValueAfter(parts, "via");
            var device = ValueAfter(parts, "dev");
            var metric = ParseInteger(ValueAfter(parts, "metric"));
            if (!isDefault && !destination.Contains('/')) destination += "/32";
            entries.Add(new RouteEntryReport(
                destination,
                includeSensitive ? via : null,
                device,
                metric,
                destination.Contains(':') ? "ipv6" : "ipv4",
                isDefault));
        }
        return AvailableRoutes(entries);
    }

    internal static RoutingDetailsReport ParseUnixRoutes(string output, bool includeSensitive, string addressFamily)
    {
        var entries = new List<RouteEntryReport>();
        foreach (var raw in Lines(output))
        {
            var parts = SplitColumns(raw);
            if (parts.Length < 4 || parts[0] is "Destination" or "Routing") continue;
            var destination = parts[0] == "default" ? "0.0.0.0/0" : parts[0];
            var isDefault = parts[0] == "default";
            entries.Add(new RouteEntryReport(
                destination,
                includeSensitive ? parts[1] : null,
                parts[^1],
                null,
                addressFamily,
                isDefault));
        }
        return AvailableRoutes(entries);
    }

    private static RoutingDetailsReport AvailableRoutes(IReadOnlyList<RouteEntryReport> entries)
    {
        return entries.Count == 0
            ? UnavailableRoutes("The route command completed but no entries could be parsed.")
            : new RoutingDetailsReport("available", entries.Take(128).ToArray(), null);
    }

    private static WifiDetailsReport UnavailableWifi(string? error) => new(
        "unavailable", null, null, null, null, null, null, null, null, null, null, error ?? "Wi-Fi details are unavailable.");

    private static RoutingDetailsReport UnavailableRoutes(string? error) => new(
        "unavailable", [], error ?? "Routing details are unavailable.");

    private static async Task<CommandResult> RunCommandAsync(
        string filename,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = filename,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = new Process { StartInfo = start };
            if (!process.Start()) return new CommandResult(false, string.Empty, $"Could not start {filename}.");
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            var error = await errorTask;
            return process.ExitCode == 0
                ? new CommandResult(true, output, null)
                : new CommandResult(false, output, string.IsNullOrWhiteSpace(error) ? $"{filename} exited with code {process.ExitCode}." : error.Trim());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CommandResult(false, string.Empty, $"{filename} timed out.");
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or IOException)
        {
            return new CommandResult(false, string.Empty, error.Message);
        }
    }

    private static Dictionary<string, string> ParseKeyValueLines(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in Lines(output))
        {
            var separator = raw.IndexOf(':');
            if (separator <= 0) continue;
            var key = raw[..separator].Trim();
            var value = raw[(separator + 1)..].Trim();
            if (key.Length > 0 && value.Length > 0) values[key] = value;
        }
        return values;
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string[] Lines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static string[] SplitColumns(string value) =>
        value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? ValueAfter(string[] parts, string marker)
    {
        var index = Array.FindIndex(parts, item => string.Equals(item, marker, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < parts.Length ? parts[index + 1] : null;
    }

    private static int? ParsePercent(string? value) => ParseInteger(value?.TrimEnd('%'));

    private static int? ParseInteger(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static long? ParseRate(string value)
    {
        var token = value.Trim().Split(' ')[0];
        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? (long)Math.Round(parsed)
            : null;
    }

    private static string? BandFromChannel(int? channel) => channel switch
    {
        null => null,
        <= 14 => "2.4 GHz",
        <= 177 => "5 GHz",
        _ => "6 GHz"
    };

    private static int? ChannelFromFrequency(int? frequency) => frequency switch
    {
        null => null,
        2484 => 14,
        >= 2412 and <= 2472 => (frequency.Value - 2407) / 5,
        >= 5000 and <= 5895 => (frequency.Value - 5000) / 5,
        >= 5955 and <= 7115 => (frequency.Value - 5950) / 5,
        _ => null
    };

    private static int PrefixLength(string mask)
    {
        if (!IPAddress.TryParse(mask, out var address)) return 0;
        var prefix = 0;
        foreach (var value in address.GetAddressBytes())
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                if ((value & (1 << bit)) != 0) prefix++;
            }
        }
        return prefix;
    }

    private sealed record CommandResult(bool Success, string Output, string? Error);
}
