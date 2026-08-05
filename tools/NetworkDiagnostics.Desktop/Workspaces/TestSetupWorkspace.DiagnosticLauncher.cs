using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private readonly Dictionary<Button, TextBlock> diagnosticSelectionBadges = new();
    private Control? diagnosticLauncherContent;
    private Button? quickLauncherButton;
    private Button? fullLauncherButton;
    private Button? stressLauncherButton;
    private Button? moreLauncherButton;
    private Button? launcherRunButton;
    private TextBlock? launcherSummaryText;
    private bool diagnosticLauncherInstalled;
    private int pendingLauncherProfileIndex = 1;

    public event EventHandler? DiagnosticLauncherRequested;

    public Control? DiagnosticLauncherContent => diagnosticLauncherContent;

    public void PrepareDiagnosticLauncherLayout()
    {
        InstallDiagnosticLauncher();

        ConfigureDiagnosticLayout(900);
        ProfileGrid.ColumnSpacing = 10;
        ProfileGrid.RowSpacing = 10;
        ProfileGrid.Margin = new Thickness(0, 2, 0, 0);

        DiagnosticDetailsGrid.ColumnDefinitions.Clear();
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.08, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.92, GridUnitType.Star)));
        DiagnosticDetailsGrid.RowDefinitions.Clear();
        DiagnosticDetailsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        DiagnosticDetailsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        DiagnosticDetailsGrid.ColumnSpacing = 26;
        DiagnosticDetailsGrid.RowSpacing = 20;

        SetGridPosition(SelectedQuestionPanel, 0, 0);
        SetGridPosition(RunPlanPanel, 0, 1);
        SetGridPosition(MethodPanel, 1, 0);
        SetGridPosition(RunActionPanel, 1, 1);

        foreach (var button in ProfileButtons())
        {
            button.MinHeight = 74;
            button.Padding = new Thickness(14, 11);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        }

        RunActionPanel.MinWidth = 0;
        RunActionPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        RunButton.MinWidth = 0;
        RunButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        RefreshDiagnosticProfileVisuals();
    }

    private void InstallDiagnosticLauncher()
    {
        if (diagnosticLauncherInstalled
            || DiagnosticExpander.Parent is not Border launcherHost
            || DiagnosticExpander.Content is not Control diagnosticContent)
        {
            return;
        }

        DiagnosticExpander.Content = null;
        if (diagnosticContent is Border diagnosticBorder)
        {
            diagnosticBorder.BorderThickness = new Thickness(0);
            diagnosticBorder.Padding = new Thickness(0);
        }

        var description = new TextBlock
        {
            Text = "Choose a profile, transfer method, and run plan. Interface and endpoint details are reviewed before the test starts.",
            FontSize = 11,
            LineHeight = 17,
            TextWrapping = TextWrapping.Wrap
        };
        description.Classes.Add("secondary");

        var overlayBody = new StackPanel
        {
            Margin = new Thickness(22, 18, 22, 24),
            Spacing = 14
        };
        overlayBody.Children.Add(description);
        overlayBody.Children.Add(diagnosticContent);

        diagnosticLauncherContent = new ScrollViewer
        {
            Content = overlayBody,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        EnsureDiagnosticSelectionBadges();
        foreach (var button in ProfileButtons())
        {
            button.Click += DiagnosticProfileVisualStateChanged;
        }

        launcherHost.Child = CreateDiagnosticLauncherSurface();
        LayoutUpdated += DiagnosticLauncherLayoutUpdated;
        diagnosticLauncherInstalled = true;
        SyncDiagnosticLauncherState();
    }

    private Control CreateDiagnosticLauncherSurface()
    {
        var title = new TextBlock
        {
            Text = "Run a deeper diagnostic",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold
        };
        launcherSummaryText = new TextBlock
        {
            Text = "Quick selected",
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        };
        launcherSummaryText.Classes.Add("muted");

        var copy = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center
        };
        copy.Children.Add(title);
        copy.Children.Add(launcherSummaryText);

        quickLauncherButton = CreateLauncherSelector("Quick", 1, 78);
        fullLauncherButton = CreateLauncherSelector("Full", 2, 74);
        stressLauncherButton = CreateLauncherSelector("Stress", 3, 82);
        moreLauncherButton = CreateLauncherAction("More…", "ghost", MoreLauncherClicked, 76);

        var selectors = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        selectors.Children.Add(quickLauncherButton);
        selectors.Children.Add(fullLauncherButton);
        selectors.Children.Add(stressLauncherButton);
        selectors.Children.Add(moreLauncherButton);

        launcherRunButton = CreateLauncherAction("Run Quick", "primary", LauncherRunClicked, 118);

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 14,
            MinHeight = 82,
            Margin = new Thickness(18, 0)
        };
        layout.Children.Add(copy);
        Grid.SetColumn(selectors, 1);
        layout.Children.Add(selectors);
        Grid.SetColumn(launcherRunButton, 2);
        layout.Children.Add(launcherRunButton);
        return layout;
    }

    private Button CreateLauncherSelector(string label, int profileIndex, double minWidth)
    {
        var button = new Button
        {
            Content = label,
            Tag = profileIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MinWidth = minWidth,
            MinHeight = 36,
            Padding = new Thickness(13, 7),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("profileChoice");
        button.Click += LauncherProfileClicked;
        return button;
    }

    private static Button CreateLauncherAction(
        string label,
        string styleClass,
        EventHandler<RoutedEventArgs> clickHandler,
        double minWidth)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = minWidth,
            MinHeight = 36,
            Padding = new Thickness(13, 7),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add(styleClass);
        button.Click += clickHandler;
        return button;
    }

    private IReadOnlyList<Button> ProfileButtons() =>
    [
        ConnectionProfileButton,
        QuickProfileButton,
        FullProfileButton,
        StressProfileButton
    ];

    private void EnsureDiagnosticSelectionBadges()
    {
        foreach (var button in ProfileButtons())
        {
            if (diagnosticSelectionBadges.ContainsKey(button)
                || button.Content is not StackPanel content)
            {
                continue;
            }

            var badge = new TextBlock
            {
                Text = "SELECTED",
                FontSize = 8,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = 1.1,
                IsVisible = false
            };
            badge.Classes.Add("eyebrow");
            content.Children.Insert(0, badge);
            diagnosticSelectionBadges[button] = badge;
        }
    }

    private void DiagnosticProfileVisualStateChanged(object? sender, RoutedEventArgs eventArgs) =>
        Dispatcher.UIThread.Post(() =>
        {
            RefreshDiagnosticProfileVisuals();
            SyncDiagnosticLauncherState();
        });

    private void RefreshDiagnosticProfileVisuals()
    {
        EnsureDiagnosticSelectionBadges();
        foreach (var button in ProfileButtons())
        {
            var selected = button.Classes.Contains("selected");
            button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            button.Opacity = selected ? 1 : 0.88;
            if (diagnosticSelectionBadges.TryGetValue(button, out var badge))
            {
                badge.IsVisible = selected;
            }
        }
    }

    private void DiagnosticLauncherLayoutUpdated(object? sender, EventArgs eventArgs) =>
        SyncDiagnosticLauncherState();

    private void SyncDiagnosticLauncherState()
    {
        if (quickLauncherButton is null
            || fullLauncherButton is null
            || stressLauncherButton is null
            || moreLauncherButton is null
            || launcherRunButton is null
            || launcherSummaryText is null)
        {
            return;
        }

        var selectedProfileIndex = SelectedProfileIndex();
        if (selectedProfileIndex is >= 1 and <= 3)
        {
            pendingLauncherProfileIndex = selectedProfileIndex;
        }

        SetSelected(quickLauncherButton, selectedProfileIndex == 1);
        SetSelected(fullLauncherButton, selectedProfileIndex == 2);
        SetSelected(stressLauncherButton, selectedProfileIndex == 3);
        ApplyMoreButtonStyle(selectedProfileIndex == 0);

        var profileName = ProfileName(selectedProfileIndex);
        var summary = $"{profileName} · {EstimatedTimeText.Text} · {TransferCapText.Text}";
        if (!string.IsNullOrWhiteSpace(ConfirmationText.Text)
            && !ConfirmationText.Text.Equals("No", StringComparison.OrdinalIgnoreCase)
            && !ConfirmationText.Text.Equals("Not required", StringComparison.OrdinalIgnoreCase))
        {
            summary += $" · confirmation {ConfirmationText.Text.ToLowerInvariant()}";
        }

        if (launcherSummaryText.Text != summary)
        {
            launcherSummaryText.Text = summary;
        }

        var controlsEnabled = RunButton.IsEnabled;
        var runLabel = controlsEnabled ? $"Run {profileName}" : "Diagnostic running";
        if (!Equals(launcherRunButton.Content, runLabel))
        {
            launcherRunButton.Content = runLabel;
        }

        SetEnabled(launcherRunButton, controlsEnabled);
        SetEnabled(quickLauncherButton, controlsEnabled);
        SetEnabled(fullLauncherButton, controlsEnabled);
        SetEnabled(stressLauncherButton, controlsEnabled);
        SetEnabled(moreLauncherButton, controlsEnabled);
    }

    private void ApplyMoreButtonStyle(bool customProfileSelected)
    {
        if (moreLauncherButton is null) return;

        var wantedClass = customProfileSelected ? "secondary" : "ghost";
        var unwantedClass = customProfileSelected ? "ghost" : "secondary";
        moreLauncherButton.Classes.Remove(unwantedClass);
        if (!moreLauncherButton.Classes.Contains(wantedClass))
        {
            moreLauncherButton.Classes.Add(wantedClass);
        }
    }

    private static void SetEnabled(Control control, bool enabled)
    {
        if (control.IsEnabled != enabled)
        {
            control.IsEnabled = enabled;
        }
    }

    private int SelectedProfileIndex()
    {
        if (QuickProfileButton.Classes.Contains("selected")) return 1;
        if (FullProfileButton.Classes.Contains("selected")) return 2;
        if (StressProfileButton.Classes.Contains("selected")) return 3;
        if (ConnectionProfileButton.Classes.Contains("selected")) return 0;
        return pendingLauncherProfileIndex;
    }

    private static string ProfileName(int profileIndex) => profileIndex switch
    {
        0 => "Connection Check",
        2 => "Full",
        3 => "Stress",
        _ => "Quick"
    };

    private void LauncherProfileClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string value }
            || !int.TryParse(value, out var profileIndex))
        {
            return;
        }

        pendingLauncherProfileIndex = profileIndex;
        SetSelected(ConnectionProfileButton, false);
        SetSelected(QuickProfileButton, profileIndex == 1);
        SetSelected(FullProfileButton, profileIndex == 2);
        SetSelected(StressProfileButton, profileIndex == 3);
        SetSelected(quickLauncherButton!, profileIndex == 1);
        SetSelected(fullLauncherButton!, profileIndex == 2);
        SetSelected(stressLauncherButton!, profileIndex == 3);
        ApplyMoreButtonStyle(false);

        var profileName = ProfileName(profileIndex);
        if (!Equals(launcherRunButton!.Content, $"Run {profileName}"))
        {
            launcherRunButton.Content = $"Run {profileName}";
        }
        launcherSummaryText!.Text = $"{profileName} selected · loading saved configuration…";
        ProfileRequested?.Invoke(this, new IndexRequestedEventArgs(profileIndex));
    }

    private void LauncherRunClicked(object? sender, RoutedEventArgs eventArgs) =>
        RunRequested?.Invoke(this, EventArgs.Empty);

    private void MoreLauncherClicked(object? sender, RoutedEventArgs eventArgs) =>
        DiagnosticLauncherRequested?.Invoke(this, EventArgs.Empty);
}
