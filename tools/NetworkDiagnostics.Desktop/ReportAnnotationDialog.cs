using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDiagnostics.Desktop.Services;

namespace NetworkDiagnostics.Desktop;

public sealed record ReportAnnotationInput(string? Label, IReadOnlyList<string> Tags);

public sealed class ReportAnnotationDialog : Window
{
    private readonly TextBox labelTextBox;
    private readonly TextBox tagsTextBox;

    public ReportAnnotationDialog(StoredReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Title = "Label saved report";
        Width = 480;
        Height = 330;
        MinWidth = 420;
        MinHeight = 300;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#1B1F20");
        Foreground = Brush.Parse("#E9E6E0");

        labelTextBox = new TextBox
        {
            Text = report.Label ?? string.Empty,
            Watermark = "Before router restart",
            MaxLength = 80
        };
        tagsTextBox = new TextBox
        {
            Text = string.Join(", ", report.Tags),
            Watermark = "Wi-Fi, VPN off, evening"
        };

        var saveButton = new Button { Content = "Save label", HorizontalAlignment = HorizontalAlignment.Right };
        saveButton.Click += (_, _) => Close(new ReportAnnotationInput(
            string.IsNullOrWhiteSpace(labelTextBox.Text) ? null : labelTextBox.Text.Trim(),
            ParseTags(tagsTextBox.Text)));
        var cancelButton = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Right };
        cancelButton.Click += (_, _) => Close(null);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, saveButton }
        };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Label this measurement",
                    FontSize = 22,
                    FontWeight = FontWeight.SemiBold
                },
                new TextBlock
                {
                    Text = "Labels and tags remain inside the local JSON report and are included when the report is exported.",
                    Foreground = Brush.Parse("#A9A49C"),
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock { Text = "LABEL", FontSize = 11, FontWeight = FontWeight.SemiBold },
                labelTextBox,
                new TextBlock { Text = "TAGS · COMMA SEPARATED", FontSize = 11, FontWeight = FontWeight.SemiBold },
                tagsTextBox,
                actions
            }
        };
        Content = new Border { Padding = new Avalonia.Thickness(24), Child = content };
        Opened += (_, _) => labelTextBox.Focus();
    }

    internal static IReadOnlyList<string> ParseTags(string? value) => string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
