using Avalonia.Controls;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private bool diagnosticConfiguratorResponsiveGuardInstalled;

    public void InstallDiagnosticConfiguratorResponsiveGuard()
    {
        if (diagnosticConfiguratorResponsiveGuardInstalled)
        {
            return;
        }

        LayoutUpdated += DiagnosticConfiguratorResponsiveLayoutUpdated;
        diagnosticConfiguratorResponsiveGuardInstalled = true;
        NormalizeDiagnosticConfiguratorProfileRail();
    }

    private void DiagnosticConfiguratorResponsiveLayoutUpdated(object? sender, EventArgs eventArgs) =>
        NormalizeDiagnosticConfiguratorProfileRail();

    private void NormalizeDiagnosticConfiguratorProfileRail()
    {
        if (!polishedDiagnosticConfiguratorBuilt)
        {
            return;
        }

        var buttons = ProfileButtons();
        var alreadyNormalized = ProfileGrid.ColumnDefinitions.Count == 1
            && ProfileGrid.RowDefinitions.Count == buttons.Count
            && buttons.Select(Grid.GetColumn).All(column => column == 0)
            && buttons.Select(Grid.GetRow).SequenceEqual(Enumerable.Range(0, buttons.Count));
        if (alreadyNormalized)
        {
            return;
        }

        ProfileGrid.ColumnDefinitions.Clear();
        ProfileGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ProfileGrid.RowDefinitions.Clear();
        for (var index = 0; index < buttons.Count; index++)
        {
            ProfileGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            SetGridPosition(buttons[index], index, 0);
        }
        ProfileGrid.ColumnSpacing = 0;
        ProfileGrid.RowSpacing = 7;
    }
}
