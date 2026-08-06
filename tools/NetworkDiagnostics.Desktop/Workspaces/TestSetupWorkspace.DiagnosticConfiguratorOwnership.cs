namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    public void ApplyDiagnosticConfiguratorPolishSafely()
    {
        if (!polishedDiagnosticConfiguratorBuilt)
        {
            DetachConfiguratorControl(RunButton);
        }
        ApplyDiagnosticConfiguratorPolish();

        if (diagnosticLauncherContent is Avalonia.Controls.Control content)
        {
            content.MinHeight = 408;
        }
    }
}
