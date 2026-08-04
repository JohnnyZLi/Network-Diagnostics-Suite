using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed record TestSetupWorkspaceModel(
    int ProfileIndex,
    int MethodIndex,
    string Question,
    string Purpose,
    string MethodDetail,
    string EstimatedTime,
    string TransferCap,
    string Confirmation,
    string Availability,
    string Interface,
    string Endpoint,
    string Network,
    bool RunActive,
    string ActiveRunTitle,
    string ActiveRunDetail,
    double ActiveRunProgress);

public sealed class IndexRequestedEventArgs(int index) : EventArgs
{
    public int Index { get; } = index;
}

public sealed partial class TestSetupWorkspace : UserControl
{
    public TestSetupWorkspace()
    {
        InitializeComponent();
    }

    public event EventHandler<IndexRequestedEventArgs>? ProfileRequested;

    public event EventHandler<IndexRequestedEventArgs>? MethodRequested;

    public event EventHandler? RunRequested;

    public event EventHandler? ActiveRunRequested;

    public event EventHandler? SettingsRequested;

    public void Render(TestSetupWorkspaceModel model)
    {
        SetSelected(ConnectionProfileButton, model.ProfileIndex == 0);
        SetSelected(QuickProfileButton, model.ProfileIndex == 1);
        SetSelected(FullProfileButton, model.ProfileIndex == 2);
        SetSelected(StressProfileButton, model.ProfileIndex == 3);
        SetSelected(CompareMethodButton, model.MethodIndex == 0);
        SetSelected(SingleMethodButton, model.MethodIndex == 1);
        SetSelected(AggregateMethodButton, model.MethodIndex == 2);

        QuestionText.Text = model.Question;
        PurposeText.Text = model.Purpose;
        MethodDetailText.Text = model.MethodDetail;
        EstimatedTimeText.Text = model.EstimatedTime;
        TransferCapText.Text = model.TransferCap;
        ConfirmationText.Text = model.Confirmation;
        AvailabilityText.Text = model.Availability;
        InterfaceText.Text = $"Interface · {Fallback(model.Interface, "Automatic")}";
        EndpointText.Text = $"Endpoint · {Fallback(model.Endpoint, "Checking")}";
        NetworkText.Text = $"Network · {Fallback(model.Network, "Unknown")}";

        ActiveRunBorder.IsVisible = model.RunActive;
        ActiveRunTitleText.Text = model.ActiveRunTitle;
        ActiveRunDetailText.Text = model.ActiveRunDetail;
        ActiveRunProgress.Value = Math.Clamp(model.ActiveRunProgress, 0, 100);
        RunButton.IsEnabled = !model.RunActive;
        RunButton.Content = model.RunActive ? "Diagnostic running" : "Run diagnostic";
    }

    private void ProfileClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string value } && int.TryParse(value, out var index))
        {
            ProfileRequested?.Invoke(this, new IndexRequestedEventArgs(index));
        }
    }

    private void MethodClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string value } && int.TryParse(value, out var index))
        {
            MethodRequested?.Invoke(this, new IndexRequestedEventArgs(index));
        }
    }

    private void RunClicked(object? sender, RoutedEventArgs eventArgs) =>
        RunRequested?.Invoke(this, EventArgs.Empty);

    private void ActiveRunClicked(object? sender, RoutedEventArgs eventArgs) =>
        ActiveRunRequested?.Invoke(this, EventArgs.Empty);

    private void SettingsClicked(object? sender, RoutedEventArgs eventArgs) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private static void SetSelected(Button button, bool selected)
    {
        if (selected)
        {
            if (!button.Classes.Contains("selected")) button.Classes.Add("selected");
        }
        else
        {
            button.Classes.Remove("selected");
        }
    }

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
