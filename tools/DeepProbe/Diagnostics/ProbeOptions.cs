using NetworkDeepProbe.Planning;

namespace NetworkDeepProbe.Diagnostics;

internal sealed record ProbeOptions(
    string Target,
    string OutputPath,
    int PingCount,
    int MaximumHops,
    bool IncludeAddresses,
    string? LanTarget,
    int LanPort,
    int LanDurationSeconds,
    int LanConcurrency,
    bool LanServer,
    bool IncludeInternetTransfer,
    TestProfileId Profile,
    TransferMethod TransferMethod,
    Uri TestOrigin,
    bool ShowHelp,
    IReadOnlyList<Uri>? AdditionalTestOrigins = null,
    string? InterfaceId = null,
    DownloadPathPreference DownloadPath = DownloadPathPreference.Automatic)
{
    public IReadOnlyList<Uri> CandidateOrigins =>
        new[] { TestOrigin }
            .Concat(AdditionalTestOrigins ?? [])
            .DistinctBy(origin => origin.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

    public static ProbeOptions Parse(string[] args, DateTimeOffset? now = null)
    {
        var target = "1.1.1.1";
        var output = $"network-report-{(now ?? DateTimeOffset.Now):yyyyMMdd-HHmmss}.json";
        var pingCount = 20;
        var maximumHops = 30;
        var includeAddresses = false;
        string? lanTarget = null;
        var lanPort = 8765;
        var lanDurationSeconds = 8;
        var lanConcurrency = 4;
        var lanServer = false;
        var includeInternetTransfer = false;
        var profile = TestProfileId.ConnectionCheck;
        var transferMethod = TransferMethod.Compare;
        var downloadPath = DownloadPathPreference.Automatic;
        var testOrigins = new List<Uri>();
        string? interfaceId = null;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--target":
                    target = RequireValue(args, ref index, "--target");
                    break;
                case "--output":
                    output = RequireValue(args, ref index, "--output");
                    break;
                case "--pings":
                    pingCount = ParseBoundedInteger(RequireValue(args, ref index, "--pings"), 5, 100, "--pings");
                    break;
                case "--max-hops":
                    maximumHops = ParseBoundedInteger(RequireValue(args, ref index, "--max-hops"), 5, 64, "--max-hops");
                    break;
                case "--include-addresses":
                    includeAddresses = true;
                    break;
                case "--interface":
                    interfaceId = RequireValue(args, ref index, "--interface");
                    break;
                case "--internet-transfer":
                    includeInternetTransfer = true;
                    break;
                case "--profile":
                    profile = NativeTransferPlanBuilder.ParseProfile(RequireValue(args, ref index, "--profile"));
                    break;
                case "--transfer-method":
                    transferMethod = NativeTransferPlanBuilder.ParseMethod(RequireValue(args, ref index, "--transfer-method"));
                    break;
                case "--download-path":
                    downloadPath = NativeTransferPlanBuilder.ParseDownloadPath(RequireValue(args, ref index, "--download-path"));
                    break;
                case "--test-origin":
                    testOrigins.Add(ParseOrigin(RequireValue(args, ref index, "--test-origin")));
                    break;
                case "--lan-target":
                    lanTarget = RequireValue(args, ref index, "--lan-target");
                    break;
                case "--lan-port":
                    lanPort = ParseBoundedInteger(RequireValue(args, ref index, "--lan-port"), 1024, 65535, "--lan-port");
                    break;
                case "--lan-duration":
                    lanDurationSeconds = ParseBoundedInteger(RequireValue(args, ref index, "--lan-duration"), 3, 30, "--lan-duration");
                    break;
                case "--lan-streams":
                    lanConcurrency = ParseBoundedInteger(RequireValue(args, ref index, "--lan-streams"), 1, 16, "--lan-streams");
                    break;
                case "--lan-server":
                    lanServer = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[index]}");
            }
        }

        if (testOrigins.Count > 8)
        {
            throw new ArgumentException("No more than eight --test-origin candidates may be configured.");
        }
        if (lanServer && lanTarget is not null)
        {
            throw new ArgumentException("--lan-server and --lan-target cannot be used together.");
        }
        if (lanServer && includeInternetTransfer)
        {
            throw new ArgumentException("--lan-server cannot be combined with --internet-transfer.");
        }

        var primaryOrigin = testOrigins.FirstOrDefault() ?? InternetTransferProbe.DefaultOrigin;
        return new ProbeOptions(
            target,
            output,
            pingCount,
            maximumHops,
            includeAddresses,
            lanTarget,
            lanPort,
            lanDurationSeconds,
            lanConcurrency,
            lanServer,
            includeInternetTransfer,
            profile,
            transferMethod,
            primaryOrigin,
            showHelp,
            testOrigins.Skip(1).ToArray(),
            interfaceId,
            downloadPath);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{option} requires a value.");
        }
        index++;
        return args[index];
    }

    private static int ParseBoundedInteger(string value, int minimum, int maximum, string option)
    {
        if (!int.TryParse(value, out var parsed) || parsed < minimum || parsed > maximum)
        {
            throw new ArgumentException($"{option} must be between {minimum} and {maximum}.");
        }
        return parsed;
    }

    private static Uri ParseOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            || parsed.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("--test-origin must be an absolute HTTP or HTTPS URL.");
        }
        var builder = new UriBuilder(parsed)
        {
            Path = parsed.AbsolutePath.EndsWith('/') ? parsed.AbsolutePath : $"{parsed.AbsolutePath}/"
        };
        return builder.Uri;
    }
}
