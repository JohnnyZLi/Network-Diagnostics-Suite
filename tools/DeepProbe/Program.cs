using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Models;

return await ProbeProgram.RunAsync(args);

internal static class ProbeProgram
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<int> RunAsync(string[] args)
    {
        ProbeOptions options;
        try
        {
            options = ProbeOptions.Parse(args);
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine(error.Message);
            Console.Error.WriteLine("Run NetworkDeepProbe --help for usage.");
            return 2;
        }

        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        if (options.LanServer)
        {
            try
            {
                await LanThroughputServer.RunAsync(options.LanPort, cancellation.Token);
                return 0;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Console.Error.WriteLine($"LAN server failed: {error.Message}");
                return 1;
            }
        }

        Console.WriteLine("Network Deep Probe");
        Console.WriteLine("Local-only collection; public IP, MAC address, hostname, and SSID are omitted.");
        if (options.IncludeInternetTransfer)
        {
            var plan = NetworkDeepProbe.Planning.NativeTransferPlanBuilder.Build(options.Profile, options.TransferMethod);
            Console.WriteLine($"Internet transfer: {plan.ProfileName} / {plan.Method} / up to {plan.TransferCapBytes:N0} bytes.");
        }
        Console.WriteLine();
        var progress = new Progress<string>(message => Console.WriteLine($"  • {message}"));

        try
        {
            object report = options.IncludeInternetTransfer
                ? await FullDiagnosticRunner.RunAsync(options, progress, cancellation.Token)
                : await ProbeRunner.RunAsync(options, progress, cancellation.Token);
            if (report is NetworkDiagnosticsReportV2 schemaTwo)
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
                report = schemaTwo with
                {
                    Producer = new ReportProducer("cli", version, "network-diagnostics-native")
                };
            }

            var outputPath = Path.GetFullPath(options.OutputPath);
            var json = JsonSerializer.Serialize(report, report.GetType(), JsonOptions);
            await File.WriteAllTextAsync(outputPath, json, cancellation.Token);
            Console.WriteLine();
            Console.WriteLine($"Report written to {outputPath}");
            Console.WriteLine(options.IncludeInternetTransfer
                ? "This schema 2.0 report contains profile-appropriate native measurements and findings."
                : "Import that schema 1.2 JSON file into the browser dashboard to view the deep results.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Probe cancelled.");
            return 130;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Probe failed: {error.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("NetworkDeepProbe [options]");
        Console.WriteLine();
        Console.WriteLine("Deep diagnostics:");
        Console.WriteLine("  --target <host>       Ping and traceroute target (default: 1.1.1.1)");
        Console.WriteLine("  --output <file>       JSON report path");
        Console.WriteLine("  --pings <5-100>       Internet ping count (default: 20)");
        Console.WriteLine("  --max-hops <5-64>     Traceroute hop limit (default: 30)");
        Console.WriteLine("  --include-addresses   Include local addresses, route gateways, and SSID");
        Console.WriteLine("  --interface <id>      Source-bind HTTP/LAN traffic to an active interface");
        Console.WriteLine();
        Console.WriteLine("First-party Internet transfer:");
        Console.WriteLine("  --internet-transfer   Add profile-driven Internet download/upload measurements");
        Console.WriteLine("  --profile <name>      connection-check, quick, full, or stress (default: connection-check)");
        Console.WriteLine("  --transfer-method <m> compare, single, or aggregate (default: compare)");
        Console.WriteLine("  --test-origin <url>   Endpoint candidate; repeat up to eight times");
        Console.WriteLine();
        Console.WriteLine("Local-link isolation (requires two machines on the same LAN):");
        Console.WriteLine("  --lan-server          Run the local throughput server until Ctrl+C");
        Console.WriteLine("  --lan-target <host>   Test against a machine running --lan-server");
        Console.WriteLine("  --lan-port <port>     Server/client TCP port (default: 8765)");
        Console.WriteLine("  --lan-duration <3-30> Seconds per transfer direction (default: 8)");
        Console.WriteLine("  --lan-streams <1-16>  Parallel TCP streams (default: 4)");
        Console.WriteLine();
        Console.WriteLine("  --help                Show this help");
        Console.WriteLine();
        Console.WriteLine("The default deep-only report is schema 1.2. --internet-transfer emits schema 2.0.");
        Console.WriteLine("The default report omits hostname, public IP, MAC address, SSID, and local addresses.");
    }
}
