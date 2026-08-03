using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NetworkDeepProbe.Planning;

namespace NetworkDiagnostics.Desktop;

internal sealed record DataUseConfirmation(bool Confirmed, bool Remember);

internal sealed class DataUseDialog : Window
{
    private readonly CheckBox rememberChoice;

    public DataUseDialog(NativeTransferPlan plan)
    {
        Title = "Confirm data use";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = ThemeBrush("AppBackgroundBrush");
        Foreground = ThemeBrush("TextPrimaryBrush");

        rememberChoice = new CheckBox
        {
            Content = $"Remember this choice for {plan.ProfileName} tests on this Mac.",
            IsChecked = true
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(16, 9),
            MinWidth = 96,
            MinHeight = 44,
            Background = ThemeBrush("SurfaceRaisedBrush"),
            Foreground = ThemeBrush("TextPrimaryBrush"),
            BorderBrush = ThemeBrush("BorderStrongBrush"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            FontWeight = FontWeight.SemiBold
        };
        cancel.Click += (_, _) => Close(new DataUseConfirmation(false, false));
        var confirm = new Button
        {
            Content = RunLabel(plan.Profile),
            Padding = new Avalonia.Thickness(16, 9),
            MinWidth = 150,
            MinHeight = 44,
            Background = ThemeBrush("AccentBrush"),
            Foreground = ThemeBrush("AccentInkBrush"),
            BorderBrush = ThemeBrush("AccentBrush"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            FontWeight = FontWeight.SemiBold
        };
        confirm.Click += (_, _) => Close(new DataUseConfirmation(true, rememberChoice.IsChecked == true));

        Content = new Border
        {
            Padding = new Avalonia.Thickness(28),
            Child = new StackPanel
            {
                Spacing = 13,
                Children =
                {
                    new Border
                    {
                        Width = 38,
                        Height = 3,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Background = ThemeBrush("AccentBrush")
                    },
                    new TextBlock
                    {
                        Text = "CONFIRM DATA USE",
                        FontFamily = new FontFamily("Menlo"),
                        FontSize = 10,
                        FontWeight = FontWeight.Bold,
                        LetterSpacing = 1.2,
                        Foreground = ThemeBrush("AccentBrush")
                    },
                    new TextBlock
                    {
                        Text = $"Run the {plan.ProfileName} test?",
                        FontSize = 26,
                        FontWeight = FontWeight.SemiBold,
                        LetterSpacing = -0.5
                    },
                    new TextBlock
                    {
                        Text = $"This test may transfer up to {FormatBytes(plan.TransferCapBytes)}. The selected {plan.Method.ToString().ToLowerInvariant()} method determines which transfer stages run. Avoid running it on metered or cellular connections.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = ThemeBrush("TextSecondaryBrush"),
                        FontSize = 14,
                        LineHeight = 21
                    },
                    rememberChoice,
                    new TextBlock
                    {
                        Text = "You’ll be asked again if this profile’s transfer cap increases.",
                        Foreground = ThemeBrush("TextTertiaryBrush"),
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children = { cancel, confirm }
                    }
                }
            }
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000d:0.###} GB";
        return $"{bytes / 1_000_000d:0.#} MB";
    }

    private static string RunLabel(TestProfileId profile) => profile switch
    {
        TestProfileId.Standard => "Run full diagnostic",
        TestProfileId.Extended => "Run stress test",
        _ => "Run connection check"
    };

    private static IBrush ThemeBrush(string key)
    {
        if (Application.Current is { } application
            && application.TryFindResource(key, application.ActualThemeVariant, out var value)
            && value is IBrush brush)
        {
            return brush;
        }
        return Brushes.Gray;
    }
}
