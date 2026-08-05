using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDiagnostics.Desktop.Presentation;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class ReportBrowserWorkspace : UserControl
{
    private IReadOnlyList<StoredReport> allReports = [];
    private StoredReport? selectedReport;
    private bool applyingState;

    public ReportBrowserWorkspace()
    {
        InitializeComponent();
        applyingState = true;
        SortSelector.SelectedIndex = 0;
        applyingState = false;
        RenderSelection();
    }

    public event EventHandler? ImportRequested;

    public event EventHandler? OpenFolderRequested;

    public event EventHandler<StoredReportEventArgs>? OpenReportRequested;

    public event EventHandler<StoredReportEventArgs>? CompareReportRequested;

    public event EventHandler<StoredReportEventArgs>? EditReportRequested;

    public event EventHandler<ReportBrowserStateChangedEventArgs>? StateChanged;

    public int VisibleReportCount { get; private set; }

    public ReportBrowserState CaptureState() => new(
        SearchBox.Text?.Trim() ?? string.Empty,
        SelectedSortKey(),
        SelectedSortKey() != "date-asc",
        selectedReport?.Report.Run.Id);

    public void Render(IReadOnlyList<StoredReport> reports, ReportBrowserState? state = null)
    {
        allReports = reports;
        if (state is not null)
        {
            ApplyState(state);
        }
        else if (selectedReport is not null)
        {
            selectedReport = reports.FirstOrDefault(item => item.Report.Run.Id == selectedReport.Report.Run.Id);
        }

        RefreshRows();
    }

    public void ApplyState(ReportBrowserState state)
    {
        applyingState = true;
        try
        {
            SearchBox.Text = state.SearchQuery;
            SortSelector.SelectedIndex = state.SortKey switch
            {
                "date-asc" => 1,
                "profile" => 2,
                "verdict" => 3,
                _ => 0
            };
            selectedReport = state.SelectedReportId is { } selectedId
                ? allReports.FirstOrDefault(item => item.Report.Run.Id == selectedId)
                : null;
        }
        finally
        {
            applyingState = false;
        }
    }

    private void SearchChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        if (applyingState) return;
        RefreshRows();
        RaiseStateChanged();
    }

    private void SortChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (applyingState) return;
        RefreshRows();
        RaiseStateChanged();
    }

    private void ImportClicked(object? sender, RoutedEventArgs eventArgs) =>
        ImportRequested?.Invoke(this, EventArgs.Empty);

    private void OpenFolderClicked(object? sender, RoutedEventArgs eventArgs) =>
        OpenFolderRequested?.Invoke(this, EventArgs.Empty);

    private void OpenSelectedClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (selectedReport is not null)
        {
            OpenReportRequested?.Invoke(this, new StoredReportEventArgs(selectedReport));
        }
    }

    private void CompareSelectedClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (selectedReport is not null)
        {
            CompareReportRequested?.Invoke(this, new StoredReportEventArgs(selectedReport));
        }
    }

    private void EditSelectedClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (selectedReport is not null)
        {
            EditReportRequested?.Invoke(this, new StoredReportEventArgs(selectedReport));
        }
    }

    private void ReportRowClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: StoredReport stored }) return;
        selectedReport = stored;
        RefreshRows();
        RaiseStateChanged();
    }

    private void RefreshRows()
    {
        var libraryEmpty = allReports.Count == 0;
        EmptyStateBorder.IsVisible = libraryEmpty;
        FilterBar.IsVisible = !libraryEmpty;
        ReportTableBorder.IsVisible = !libraryEmpty;

        var query = SearchBox.Text?.Trim() ?? string.Empty;
        IEnumerable<StoredReport> filtered = allReports;
        if (query.Length > 0)
        {
            filtered = filtered.Where(report => SearchText(report).Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        filtered = SelectedSortKey() switch
        {
            "date-asc" => filtered.OrderBy(item => item.Report.GeneratedAt),
            "profile" => filtered.OrderBy(item => item.ProfileName).ThenByDescending(item => item.Report.GeneratedAt),
            "verdict" => filtered.OrderBy(item => DiagnosticReportPresenter.FromReport(item.Report).Verdict)
                .ThenByDescending(item => item.Report.GeneratedAt),
            _ => filtered.OrderByDescending(item => item.Report.GeneratedAt)
        };

        var visibleReports = filtered.ToArray();
        VisibleReportCount = visibleReports.Length;
        VisibleCountText.Text = visibleReports.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ReportListPanel.Children.Clear();

        if (!libraryEmpty && visibleReports.Length == 0)
        {
            var empty = new StackPanel
            {
                Margin = new Avalonia.Thickness(32, 70),
                Spacing = 7,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            empty.Children.Add(new TextBlock
            {
                Text = "No matching reports",
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center
            });
            var detail = new TextBlock
            {
                Text = "Try a profile, verdict, label, tag, interface, or network name.",
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            detail.Classes.Add("muted");
            empty.Children.Add(detail);
            ReportListPanel.Children.Add(empty);
        }
        else
        {
            foreach (var stored in visibleReports)
            {
                ReportListPanel.Children.Add(BuildRow(stored));
            }
        }

        if (selectedReport is not null && allReports.All(item => item.Report.Run.Id != selectedReport.Report.Run.Id))
        {
            selectedReport = null;
        }
        RenderSelection();
        RenderLibrarySummary();
    }

    private Button BuildRow(StoredReport stored)
    {
        var presentation = DiagnosticReportPresenter.FromReport(stored.Report);
        var resultPanel = new StackPanel { Spacing = 3 };
        resultPanel.Children.Add(new TextBlock
        {
            Text = stored.Label ?? presentation.Verdict,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (!string.IsNullOrWhiteSpace(stored.Label))
        {
            var verdictText = new TextBlock
            {
                Text = presentation.Verdict,
                FontSize = 9,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            verdictText.Classes.Add("muted");
            resultPanel.Children.Add(verdictText);
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("148,112,*,300"),
            ColumnSpacing = 14
        };
        grid.Children.Add(new TextBlock
        {
            Text = stored.Report.GeneratedAt.ToLocalTime().ToString("MMM d, h:mm tt"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        });
        var profile = new TextBlock
        {
            Text = stored.ProfileName,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        profile.Classes.Add("muted");
        Grid.SetColumn(profile, 1);
        grid.Children.Add(profile);
        Grid.SetColumn(resultPanel, 2);
        grid.Children.Add(resultPanel);
        var context = new TextBlock
        {
            Text = ReportComparisonService.ContextLabel(stored.Report),
            FontSize = 9,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        context.Classes.Add("muted");
        Grid.SetColumn(context, 3);
        grid.Children.Add(context);

        var button = new Button
        {
            Content = grid,
            Tag = stored,
            MinHeight = 54
        };
        button.Classes.Add("dataRow");
        if (selectedReport?.Report.Run.Id == stored.Report.Run.Id)
        {
            button.Classes.Add("selected");
        }
        button.Click += ReportRowClicked;
        return button;
    }

    private void RenderSelection()
    {
        var enabled = selectedReport is not null;
        SelectionPanel.IsVisible = enabled;
        SelectionHintBorder.IsVisible = !enabled && allReports.Count > 0;
        OpenSelectedButton.IsEnabled = enabled;
        CompareSelectedButton.IsEnabled = enabled;
        EditSelectedButton.IsEnabled = enabled;

        if (selectedReport is null)
        {
            SelectedTitleText.Text = string.Empty;
            SelectedMetaText.Text = string.Empty;
            SelectedContextText.Text = string.Empty;
            SelectedTagsText.Text = string.Empty;
            return;
        }

        var presentation = DiagnosticReportPresenter.FromReport(selectedReport.Report);
        SelectedTitleText.Text = selectedReport.Label ?? presentation.Verdict;
        SelectedMetaText.Text = $"{selectedReport.ProfileName} · {selectedReport.DisplayDate}\n{presentation.Label}";
        SelectedContextText.Text = ReportComparisonService.ContextLabel(selectedReport.Report);
        SelectedTagsText.Text = selectedReport.Tags.Count == 0
            ? "No tags"
            : string.Join(" · ", selectedReport.Tags);
        EditSelectedButton.Content = selectedReport.Label is null && selectedReport.Tags.Count == 0
            ? "Add label"
            : "Edit label";
    }

    private void RenderLibrarySummary()
    {
        var profileCount = allReports.Select(item => item.Report.Run.Profile).Distinct().Count();
        var trend = ReportComparisonService.AnalyzeTrend(allReports);
        LibrarySummaryText.Text = allReports.Count == 0
            ? "The library is empty."
            : $"{allReports.Count} saved · {profileCount} profile type{(profileCount == 1 ? string.Empty : "s")}\n{trend.CompatibleRuns} in the latest trend set";
    }

    private void RaiseStateChanged() =>
        StateChanged?.Invoke(this, new ReportBrowserStateChangedEventArgs(CaptureState()));

    private string SelectedSortKey() =>
        SortSelector.SelectedItem is ComboBoxItem { Tag: string key } ? key : "date-desc";

    private static string SearchText(StoredReport stored)
    {
        var presentation = DiagnosticReportPresenter.FromReport(stored.Report);
        return string.Join(
            ' ',
            new[]
            {
                stored.Label,
                stored.ProfileName,
                presentation.Label,
                presentation.Verdict,
                presentation.Summary,
                ReportComparisonService.ContextLabel(stored.Report),
                string.Join(' ', stored.Tags)
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
