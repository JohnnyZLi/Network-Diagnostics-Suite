using System.Drawing;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Planning;
using Photino.NET;
using Photino.NET.Server;

namespace NetworkDiagnostics.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.Ordinal))
        {
            var plan = NetworkDiagnosticsRunner.DescribePlan(TestProfileId.Quick, TransferMethod.Compare);
            Console.WriteLine($"Network Diagnostics Desktop smoke test: {plan.ProfileName} / {plan.Method} / {plan.TransferCapBytes}");
            return plan.DownloadStages.Count > 0 && plan.UploadStages.Count > 0 ? 0 : 1;
        }

        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        ConfigureHeadlessLinuxWebKit();

        var settingsRoot = new PhotinoSettingsStore().RootDirectory;
        using var singleInstance = args.Contains("--allow-multiple-instances", StringComparer.Ordinal)
            ? null
            : DesktopSingleInstance.TryAcquire(settingsRoot);
        if (singleInstance is null && !args.Contains("--allow-multiple-instances", StringComparer.Ordinal))
        {
            Console.Error.WriteLine("Network Diagnostics is already running for this user.");
            return 0;
        }

        var windowSize = InitialWindowSize(args);
        var serverArgs = args.Where(argument => !IsDesktopLaunchArgument(argument)).ToArray();

        var server = PhotinoServer.CreateStaticFileServer(
            serverArgs,
            startPort: 8210,
            portRange: 40,
            webRootFolder: "wwwroot",
            out var baseUrl);
        server.StartAsync().GetAwaiter().GetResult();

        var bridge = new PhotinoDesktopBridge();
        try
        {
            var window = new PhotinoWindow()
                .SetTitle("Network Diagnostics")
                .SetUseOsDefaultSize(false)
                .SetSize(windowSize)
                .SetMinSize(960, 700)
                .Center()
                .SetResizable(true);

            var iconPath = Path.Combine(AppContext.BaseDirectory, "AppIcon.png");
            if (File.Exists(iconPath))
            {
                window.SetIconFile(iconPath);
            }

            if (OperatingSystem.IsMacOS())
            {
                // WindowCreated can precede AppKit's final title-bar layout. Apply the
                // unified style as soon as possible, then keep it synchronized through
                // focus/size changes. A final UI-thread pass immediately after Load below
                // establishes the first visible traffic-light position before the user
                // needs to move or resize the window.
                window.WindowCreatedHandler = (_, _) => MacWindowChrome.TryEnableUnifiedTitlebar();
                window.WindowFocusInHandler = (_, _) => MacWindowChrome.TryEnableUnifiedTitlebar();
                window.WindowSizeChangedHandler = (_, _) => MacWindowChrome.TryEnableUnifiedTitlebar();
                MacWindowChrome.RegisterNativeMessageHandler(window);
            }

            bridge.Attach(window);
            using var lifetime = new DesktopLifetime(window.Close);
            window.Load(BuildLaunchUrl(baseUrl, args));
            if (OperatingSystem.IsMacOS())
            {
                MacWindowChrome.TryEnableUnifiedTitlebar();
            }
            window.WaitForClose();
            return 0;
        }
        finally
        {
            bridge.Dispose();
            server.StopAsync().GetAwaiter().GetResult();
            server.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static void ConfigureHeadlessLinuxWebKit()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        if (!isCi || string.IsNullOrWhiteSpace(display) || !display.StartsWith(':'))
        {
            return;
        }

        // Xvfb provides no usable DRI3 device in our GitHub Actions visual/soak jobs.
        // WebKitGTK accelerated compositing can therefore leave resized webviews gray
        // or partially blank even though the DOM is still responsive. Disable only in
        // headless Linux CI; real desktop sessions keep the normal renderer.
        Environment.SetEnvironmentVariable("WEBKIT_DISABLE_COMPOSITING_MODE", "1");
    }

    private static Size InitialWindowSize(string[] args)
    {
        var value = OptionValue(args, "window-size");
        if (value is null)
        {
            return new Size(1360, 860);
        }

        var dimensions = value.Split('x', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (dimensions.Length != 2
            || !int.TryParse(dimensions[0], out var width)
            || !int.TryParse(dimensions[1], out var height))
        {
            return new Size(1360, 860);
        }

        return new Size(Math.Clamp(width, 900, 2560), Math.Clamp(height, 680, 1600));
    }

    private static string BuildLaunchUrl(string baseUrl, string[] args)
    {
        var builder = new UriBuilder(baseUrl);
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Query))
        {
            query.Add(builder.Query.TrimStart('?'));
        }

        AddAllowedOption(query, args, "appearance", ["system", "light", "dark"]);
        AddAllowedOption(query, args, "profile", ["connection-check", "quick", "full", "stress"]);
        AddAllowedOption(query, args, "method", ["compare", "single", "aggregate"]);
        AddAllowedOption(query, args, "download-path", ["automatic", "direct-r2", "worker"]);
        AddAllowedOption(query, args, "panel", ["history", "alerts", "settings"]);
        AddAllowedOption(query, args, "advanced-tool", ["configuration", "lan"]);
        AddAllowedOption(query, args, "run", ["connection-check"]);
        AddAllowedOption(query, args, "open-interface-picker", ["run", "advanced"]);

        builder.Query = string.Join('&', query.Where(value => !string.IsNullOrWhiteSpace(value)));
        var workspace = OptionValue(args, "workspace");
        builder.Fragment = workspace switch
        {
            "health" => "live-network-health",
            "diagnostics" => "run-diagnostics",
            "advanced" => "advanced-diagnostics",
            _ => builder.Fragment,
        };
        return builder.Uri.AbsoluteUri;
    }

    private static void AddAllowedOption(List<string> query, string[] args, string name, string[] allowed)
    {
        var value = OptionValue(args, name);
        if (value is null || !allowed.Contains(value, StringComparer.Ordinal))
        {
            return;
        }

        query.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
    }

    private static string? OptionValue(string[] args, string name)
    {
        var prefix = $"--{name}=";
        return args.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..]
            .Trim()
            .ToLowerInvariant();
    }

    private static bool IsDesktopLaunchArgument(string argument)
    {
        string[] names = ["window-size", "workspace", "appearance", "profile", "method", "download-path", "panel", "advanced-tool", "run", "open-interface-picker"];
        return string.Equals(argument, "--allow-multiple-instances", StringComparison.Ordinal)
            || names.Any(name => argument.StartsWith($"--{name}=", StringComparison.Ordinal));
    }
}
