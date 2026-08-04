using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed record TestConfigurationModel(
    IReadOnlyList<string> Interfaces,
    int SelectedInterfaceIndex,
    bool IncludeIdentifiers,
    string Endpoint,
    string Network);

public sealed partial class TestConfigurationPanel : UserControl
{
    private bool rendering;

    public TestConfigurationPanel()
    {
        InitializeComponent();
    }

    public event EventHandler<IndexRequestedEventArgs>? InterfaceRequested;

    public event EventHandler? IdentifiersChanged;

    public event EventHandler? RefreshRequested;

    public event EventHandler? SettingsRequested;

    public bool IncludeIdentifiers => IdentifiersCheckBox.IsChecked == true;

    public void Render(TestConfigurationModel model)
    {
        rendering = true;
        try
        {
            InterfaceComboBox.Items.Clear();
            foreach (var label in model.Interfaces)
            {
                InterfaceComboBox.Items.Add(new ComboBoxItem { Content = label });
            }
            InterfaceComboBox.SelectedIndex = model.Interfaces.Count == 0
                ? -1
                : Math.Clamp(model.SelectedInterfaceIndex, 0, model.Interfaces.Count - 1);
            IdentifiersCheckBox.IsChecked = model.IncludeIdentifiers;
            EndpointText.Text = Fallback(model.Endpoint, "Checking");
            NetworkText.Text = Fallback(model.Network, "Unknown");
        }
        finally
        {
            rendering = false;
        }
    }

    private void InterfaceSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (!rendering && InterfaceComboBox.SelectedIndex >= 0)
        {
            InterfaceRequested?.Invoke(this, new IndexRequestedEventArgs(InterfaceComboBox.SelectedIndex));
        }
    }

    private void IdentifiersClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (!rendering)
        {
            IdentifiersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RefreshClicked(object? sender, RoutedEventArgs eventArgs) =>
        RefreshRequested?.Invoke(this, EventArgs.Empty);

    private void SettingsClicked(object? sender, RoutedEventArgs eventArgs) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
