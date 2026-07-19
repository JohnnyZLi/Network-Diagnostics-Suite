namespace NetworkDeepProbe.Diagnostics;

internal sealed record ProbeOptions(
    string Target,
    string OutputPath,
    int PingCount,
    int MaximumHops,
    bool IncludeAddresses,
    bool ShowHelp)
{
    public static ProbeOptions Parse(string[] args, DateTimeOffset? now = null)
    {
        var target = "1.1.1.1";
        var output = $"network-report-{(now ?? DateTimeOffset.Now):yyyyMMdd-HHmmss}.json";
        var pingCount = 20;
        var maximumHops = 30;
        var includeAddresses = false;
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
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[index]}");
            }
        }

        return new ProbeOptions(target, output, pingCount, maximumHops, includeAddresses, showHelp);
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
}
