namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    public void ApplyDiagnosticConfiguratorPolishSafely()
    {
        DetachConfiguratorControl(RunButton);
        ApplyDiagnosticConfiguratorPolish();
    }
}
