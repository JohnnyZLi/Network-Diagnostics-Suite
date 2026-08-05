using Avalonia.Controls;

namespace NetworkDiagnostics.Desktop.Workspaces;

internal static class ControlScrollExtensions
{
    public static void BringIntoView(this Control control)
    {
        // Avalonia does not expose a cross-platform BringIntoView API on Control.
        // The section remains expanded in the existing page scroller so the user
        // can reach it naturally without platform-specific scroll manipulation.
    }
}
