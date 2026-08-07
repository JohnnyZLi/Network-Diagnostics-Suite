using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class SettingsWorkspace
{
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

        foreach (var textBox in SettingsRootGrid
                     .GetLogicalDescendants()
                     .OfType<TextBox>())
        {
            textBox.MinHeight = ReferenceEquals(textBox, OriginsTextBox) ? 104 : 38;
            textBox.Padding = new Thickness(11, 7);
            textBox.CornerRadius = new CornerRadius(9);
        }
    }
}
