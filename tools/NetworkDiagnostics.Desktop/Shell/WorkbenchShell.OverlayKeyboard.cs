using Avalonia.Input;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class WorkbenchShell
{
    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (eventArgs.Handled || eventArgs.Key != Key.Escape || !OverlayOpen)
        {
            return;
        }

        OverlayCloseRequested?.Invoke(this, EventArgs.Empty);
        eventArgs.Handled = true;
    }
}
