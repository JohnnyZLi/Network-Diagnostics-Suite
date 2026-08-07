namespace NetworkDiagnostics.Desktop.Workspaces;

internal static class WorkspaceLayoutMetrics
{
    public const double ControlCenterMaxWidth = 1440;
    public const double ReportDetailMaxWidth = 1320;
    public const double ReportLibraryMaxWidth = 1440;
    public const double ComparisonMaxWidth = 1440;
    public const double SettingsMaxWidth = 1200;

    public static double HorizontalGutter(double width) =>
        width < 760 ? 16 : width < 1100 ? 24 : 28;

    public static double BottomInset(double width) =>
        width < 760 ? 28 : 40;
}
