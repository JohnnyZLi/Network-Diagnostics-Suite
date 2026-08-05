using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class SettingsWorkspace
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        ApplyComponentPolish();
        LayoutUpdated += SettingsComponentLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        LayoutUpdated -= SettingsComponentLayoutUpdated;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void SettingsComponentLayoutUpdated(object? sender, EventArgs eventArgs) =>
        ApplyComponentPolish();

    private void ApplyComponentPolish()
    {
        foreach (var button in new[]
                 {
                     GeneralButton,
                     MonitoringButton,
                     MeasurementButton,
                     PrivacyButton,
                     StorageButton,
                     DeveloperButton
                 })
        {
            button.Height = 34;
            button.MinHeight = 34;
            button.Padding = new Thickness(13, 0);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
        }

        foreach (var comboBox in SettingsRootGrid
                     .GetLogicalDescendants()
                     .OfType<ComboBox>())
        {
            comboBox.MinHeight = 38;
            comboBox.Padding = new Thickness(12, 0);
            comboBox.CornerRadius = new CornerRadius(9);
            comboBox.MaxDropDownHeight = 300;
            comboBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            comboBox.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            comboBox.VerticalContentAlignment = VerticalAlignment.Center;
        }
    }
}
