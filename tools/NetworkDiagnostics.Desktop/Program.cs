using Avalonia;
using Avalonia.Fonts.Inter;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Planning;

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

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
