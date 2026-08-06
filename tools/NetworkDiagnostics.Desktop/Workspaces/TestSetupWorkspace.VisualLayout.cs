using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NetworkDiagnostics.Desktop.Monitoring;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private Button? sevenDaysButton;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        SizeChanged += VisualLayoutSizeChanged;
        CaptureTestHubHost();
        InstallDiagnosticLauncher();
        DisableLegacyLayoutRefreshLoops();
        InstallTestHubLayout();
        EnsureSevenDayButton();
        PolishRangeSelector();
        ApplyRenderedVisualLayout(Bounds.Width);
        RefreshModelDependentVisuals();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SizeChanged -= VisualLayoutSizeChanged;
        base.OnDetachedFromVisualTree(eventArgs);
    }

    private void VisualLayoutSizeChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyRenderedVisualLayout(eventArgs.NewSize.Width);

    private void ApplyRenderedVisualLayout(double width)
    {
        var available = Math.Max(720, width - 48);
        OverviewGrid.Width = Math.Min(1360, available);

        DiagnosticExpander.Background = Brushes.Transparent;
        DiagnosticDetailsGrid.Background = Brushes.Transparent;
        ProfileGrid.Background = Brushes.Transparent;

        CompareMethodButton.Padding = new Thickness(5, 5);
        SingleMethodButton.Padding = new Thickness(5, 5);
        AggregateMethodButton.Padding = new Thickness(5, 5);
        CompareMethodButton.FontSize = 10;
        SingleMethodButton.FontSize = 10;
        AggregateMethodButton.FontSize = 10;

        ApplyTestHubResponsiveLayout(width);
        ApplyOverviewResponsiveLayout(width);

        // Once the profile controls have moved into the overlay, the legacy inline
        // responsive layout must not reparent them again during a window resize.
        if (diagnosticConfiguratorBuilt || polishedDiagnosticConfiguratorBuilt)
        {
            NormalizeDiagnosticConfiguratorProfileRail();
            return;
        }

        if (width < 1180) return;

        DiagnosticDetailsGrid.ColumnDefinitions.Clear();
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.25, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.1, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.8, GridUnitType.Star)));
        DiagnosticDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
    }

    private void ApplyOverviewResponsiveLayout(double width)
    {
        if (width >= 960) return;

        OverviewGrid.ColumnDefinitions.Clear();
        OverviewGrid.RowDefinitions.Clear();
        OverviewGrid.RowSpacing = 12;

        if (width >= 880)
        {
            OverviewGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(280)));
            OverviewGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            OverviewGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            OverviewGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            OverviewGrid.ColumnSpacing = 16;
            SetGridPosition(DeviceHeader, 0, 0);
            SetGridPosition(TelemetryHeader, 0, 1);
            SetGridPosition(ScoreColumn, 1, 0);
            SetGridPosition(TelemetryColumn, 1, 1);
            TelemetryHeader.Margin = new Thickness(2, 0, 2, 0);
            return;
        }

        OverviewGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var index = 0; index < 4; index++)
        {
            OverviewGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        OverviewGrid.ColumnSpacing = 0;
        SetGridPosition(DeviceHeader, 0, 0);
        SetGridPosition(ScoreColumn, 1, 0);
        SetGridPosition(TelemetryHeader, 2, 0);
        SetGridPosition(TelemetryColumn, 3, 0);
        TelemetryHeader.Margin = new Thickness(2, 10, 2, 0);
    }

    private void PolishRangeSelector()
    {
        var buttons = new[]
        {
            OneMinuteButton,
            FiveMinutesButton,
            OneHourButton,
            TwentyFourHoursButton,
            sevenDaysButton
        }.Where(button => button is not null).Cast<Button>().ToArray();

        if (buttons.Length == 0) return;
        if (buttons[0].GetVisualParent() is StackPanel rangePanel)
        {
            rangePanel.Spacing = 0;
            rangePanel.VerticalAlignment = VerticalAlignment.Center;
            if (rangePanel.GetVisualParent() is Border rangeSurface)
            {
                rangeSurface.Padding = new Thickness(3);
                rangeSurface.CornerRadius = new CornerRadius(10);
                rangeSurface.VerticalAlignment = VerticalAlignment.Bottom;
            }
        }

        foreach (var button in buttons)
        {
            button.Height = 32;
            button.MinHeight = 32;
            button.MinWidth = button == TwentyFourHoursButton ? 84 : 68;
            button.Padding = new Thickness(10, 0);
            button.CornerRadius = new CornerRadius(7);
            button.FontSize = 10;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
        }
    }

    private void EnsureSevenDayButton()
    {
        if (sevenDaysButton is not null
            || TwentyFourHoursButton.GetVisualParent() is not StackPanel rangePanel)
        {
            return;
        }

        sevenDaysButton = new Button
        {
            Content = "7 days",
            Tag = "7d"
        };
        sevenDaysButton.Classes.Add("range");
        sevenDaysButton.Click += SevenDaysClicked;
        rangePanel.Children.Add(sevenDaysButton);
    }

    private void SevenDaysClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        SetSelectedStable(OneMinuteButton, false);
        SetSelectedStable(FiveMinutesButton, false);
        SetSelectedStable(OneHourButton, false);
        SetSelectedStable(TwentyFourHoursButton, false);
        if (sevenDaysButton is not null) SetSelectedStable(sevenDaysButton, true);
        MonitorWindowRequested?.Invoke(this, new MonitorWindowRequestedEventArgs(MonitorWindow.SevenDays));
    }

    private void SyncSevenDaySelection()
    {
        if (sevenDaysButton is null) return;
        var shorterWindowSelected = OneMinuteButton.Classes.Contains("selected")
            || FiveMinutesButton.Classes.Contains("selected")
            || OneHourButton.Classes.Contains("selected")
            || TwentyFourHoursButton.Classes.Contains("selected");
        SetSelectedStable(sevenDaysButton, !shorterWindowSelected);
    }
}
