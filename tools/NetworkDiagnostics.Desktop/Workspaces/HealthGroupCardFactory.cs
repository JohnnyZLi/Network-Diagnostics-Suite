using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop.Workspaces;

internal static class HealthGroupCardFactory
{
    public static Border Build(HealthGroupPresentation group, bool compact = false)
    {
        var indicator = new Border
        {
            Width = compact ? 7 : 8,
            Height = compact ? 7 : 8,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center
        };
        indicator.Classes.Add(group.Tone switch
        {
            HealthGroupTone.Positive => "indicatorSuccess",
            HealthGroupTone.Attention => "indicatorAccent",
            _ => "indicatorNeutral"
        });

        var title = new TextBlock
        {
            Text = group.Title,
            FontSize = compact ? 12 : 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var state = new TextBlock
        {
            Text = group.State,
            FontSize = compact ? 9 : 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        state.Classes.Add(group.Tone == HealthGroupTone.Attention ? "secondary" : "muted");

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = compact ? 8 : 9
        };
        header.Children.Add(indicator);
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        Grid.SetColumn(state, 2);
        header.Children.Add(state);

        var summary = new TextBlock
        {
            Text = group.Summary,
            FontSize = compact ? 16 : 18,
            FontWeight = FontWeight.SemiBold,
            LineHeight = compact ? 20 : 22,
            TextWrapping = TextWrapping.Wrap
        };
        var detail = new TextBlock
        {
            Text = group.Detail,
            FontSize = compact ? 10 : 11,
            LineHeight = compact ? 15 : 17,
            TextWrapping = TextWrapping.Wrap
        };
        detail.Classes.Add("muted");

        var content = new StackPanel { Spacing = compact ? 6 : 8 };
        content.Children.Add(header);
        content.Children.Add(summary);
        content.Children.Add(detail);

        if (group.Metrics.Count > 0)
        {
            var metricRow = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                ColumnSpacing = compact ? 10 : 12,
                Margin = new Thickness(0, compact ? 3 : 5, 0, 0)
            };
            var visibleMetrics = group.Metrics.Take(2).ToArray();
            for (var index = 0; index < visibleMetrics.Length; index++)
            {
                var metric = visibleMetrics[index];
                var metricLabel = new TextBlock
                {
                    Text = metric.Label.ToUpperInvariant(),
                    FontSize = compact ? 8 : 9,
                    FontWeight = FontWeight.SemiBold,
                    LetterSpacing = compact ? 1 : 1.15
                };
                metricLabel.Classes.Add("muted");
                var metricValue = new TextBlock
                {
                    Text = metric.Value,
                    FontSize = compact ? 13 : 14,
                    FontWeight = FontWeight.SemiBold,
                    Opacity = metric.WasMeasured ? 1 : 0.62
                };
                var metricStack = new StackPanel { Spacing = 2 };
                metricStack.Children.Add(metricLabel);
                metricStack.Children.Add(metricValue);
                Grid.SetColumn(metricStack, index);
                metricRow.Children.Add(metricStack);
            }
            content.Children.Add(metricRow);
        }

        var card = new Border
        {
            MinHeight = compact ? 142 : 170,
            Padding = new Thickness(compact ? 15 : 17),
            Child = content
        };
        card.Classes.Add(group.Tone == HealthGroupTone.Attention ? "accentSurface" : "surface");
        return card;
    }

    public static void ApplyOutcomeIndicator(Border indicator, ConnectionCheckOutcome outcome)
    {
        indicator.Classes.Remove("indicatorSuccess");
        indicator.Classes.Remove("indicatorAccent");
        indicator.Classes.Remove("indicatorNeutral");
        indicator.Classes.Add(outcome switch
        {
            ConnectionCheckOutcome.Healthy => "indicatorSuccess",
            ConnectionCheckOutcome.Problematic or ConnectionCheckOutcome.Failed => "indicatorAccent",
            _ => "indicatorNeutral"
        });
    }

    public static void ApplyResponsiveLayout(
        Grid grid,
        IReadOnlyList<Control> hosts,
        double width)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        if (width >= 760)
        {
            for (var index = 0; index < 3; index++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            }
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var index = 0; index < hosts.Count; index++)
            {
                Grid.SetColumn(hosts[index], index);
                Grid.SetRow(hosts[index], 0);
                Grid.SetColumnSpan(hosts[index], 1);
            }
            return;
        }

        if (width >= 520)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(hosts[0], 0);
            Grid.SetRow(hosts[0], 0);
            Grid.SetColumnSpan(hosts[0], 1);
            Grid.SetColumn(hosts[1], 1);
            Grid.SetRow(hosts[1], 0);
            Grid.SetColumnSpan(hosts[1], 1);
            Grid.SetColumn(hosts[2], 0);
            Grid.SetRow(hosts[2], 1);
            Grid.SetColumnSpan(hosts[2], 2);
            return;
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        for (var index = 0; index < hosts.Count; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(hosts[index], 0);
            Grid.SetRow(hosts[index], index);
            Grid.SetColumnSpan(hosts[index], 1);
        }
    }
}