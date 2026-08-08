using Avalonia.Input;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class WorkbenchShell
{
    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        // Focused report/settings surfaces are normal navigation destinations rather
        // than dismissible modals. Escape remains available to transient UI such as
        // the command palette; workspace navigation uses Back/Home/explicit actions.
        base.OnKeyDown(eventArgs);
    }
}
