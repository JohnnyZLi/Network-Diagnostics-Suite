using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace NetworkDiagnostics.Desktop.Workspaces;

public sealed partial class TestSetupWorkspace
{
    private void ApplyCompactScorePresentation(double width)
    {
        var compact = width < 960;
        if (ScoreAura.GetLogicalParent() is not Grid scoreOrbGrid) return;

        var scoreCard = ScoreColumn.Children
            .OfType<Border>()
            .FirstOrDefault(border => border.Classes.Contains("dashboardPanel"));
        var scoreContent = scoreCard?.Child as StackPanel;
        var componentGrid = ResponsivenessScoreText.GetLogicalParent()?.GetLogicalParent() as Grid;
        var outerOrb = scoreOrbGrid.Children
            .OfType<Border>()
            .FirstOrDefault(border => !ReferenceEquals(border, ScoreAura));
        var innerOrb = ScoreRing.Child as Border;
        var orbTextGrid = ScoreOrbCore.Child as Grid;
        var scoreLabel = orbTextGrid?.Children
            .OfType<TextBlock>()
            .FirstOrDefault(block => !ReferenceEquals(block, OverallScoreText)
                && !ReferenceEquals(block, OverallStatusText));

        if (scoreCard is not null)
        {
            scoreCard.Padding = compact ? new Thickness(14) : new Thickness(18);
        }
        if (scoreContent is not null)
        {
            scoreContent.Spacing = compact ? 10 : 15;
        }

        scoreOrbGrid.Height = compact ? 178 : 230;
        SetCircleSize(ScoreAura, compact ? 176 : 228);
        if (outerOrb is not null) SetCircleSize(outerOrb, compact ? 164 : 210);
        SetCircleSize(ScoreRing, compact ? 152 : 194);
        if (innerOrb is not null) SetCircleSize(innerOrb, compact ? 144 : 186);
        SetCircleSize(ScoreOrbCore, compact ? 134 : 174);

        if (orbTextGrid is not null)
        {
            orbTextGrid.MinWidth = compact ? 104 : 132;
        }
        if (scoreLabel is not null)
        {
            scoreLabel.FontSize = compact ? 8 : 9;
            scoreLabel.LetterSpacing = compact ? 0.65 : 0.8;
        }
        OverallScoreText.FontSize = compact ? 44 : 58;
        OverallScoreText.LineHeight = compact ? 48 : 62;
        OverallStatusText.FontSize = compact ? 10.5 : 12;
        OverallSummaryText.FontSize = compact ? 11 : 12;
        OverallSummaryText.LineHeight = compact ? 16 : 18;
        OverallSummaryText.Margin = compact ? new Thickness(2, 0) : new Thickness(8, 0);

        if (componentGrid is not null)
        {
            // The detailed responsiveness, reliability, and speed cards are immediately
            // adjacent at this breakpoint, so repeating the three scores here only adds
            // vertical bulk without adding information.
            componentGrid.IsVisible = !compact;
        }

        AlertsPanel.IsVisible = !compact;
        MarkAlertsReadButton.IsVisible = !compact;
        ClearAlertsButton.IsVisible = !compact;
    }

    private static void SetCircleSize(Border border, double size)
    {
        border.Width = size;
        border.Height = size;
        border.CornerRadius = new CornerRadius(size / 2d);
    }
}
