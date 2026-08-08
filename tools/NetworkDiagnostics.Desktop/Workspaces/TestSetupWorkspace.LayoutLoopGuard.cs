namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    public void DisableLegacyLayoutRefreshLoops()
    {
        LayoutUpdated -= DiagnosticLauncherLayoutUpdated;
        LayoutUpdated -= PolishedConfiguratorLayoutUpdated;
        LayoutUpdated -= DiagnosticConfiguratorResponsiveLayoutUpdated;
    }
}
