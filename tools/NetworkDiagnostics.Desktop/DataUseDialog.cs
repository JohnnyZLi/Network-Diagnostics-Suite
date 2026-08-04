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
        Width = 540;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#15191A");
        Foreground = Brush.Parse("#E9E6E0");

        rememberChoice = new CheckBox
        {
            Content = $"Remember approval for the current {plan.ProfileName} transfer ceiling.",
            IsChecked = true,
            Foreground = Brush.Parse("#D8D3CB")
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(16, 10),
            MinWidth = 96,
            Background = Brushes.Transparent,
            Foreground = Brush.Parse("#D8D3CB"),
            BorderBrush = Brush.Parse("#49443D"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(10)
        };
        cancel.Click += (_, _) => Close(new DataUseConfirmation(false, false));
        var confirm = new Button
        {
            Content = $"Run {plan.ProfileName}",
            Padding = new Avalonia.Thickness(17, 11),
            MinWidth = 150,
            Background = Brush.Parse("#C96346"),
            Foreground = Brushes.White,
            BorderThickness = new Avalonia.Thickness(0),
            CornerRadius = new Avalonia.CornerRadius(10)
        };
        confirm.Click += (_, _) => Close(new DataUseConfirmation(true, rememberChoice.IsChecked == true));

        Content = new Border
        {
            Margin = new Avalonia.Thickness(1),
            Padding = new Avalonia.Thickness(28),
            Background = Brush.Parse("#1D1A13"),
            BorderBrush = Brush.Parse("#403724"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(18),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = "CONFIRM DATA USE",
                        FontSize = 11,
                        FontWeight = FontWeight.SemiBold,
                        LetterSpacing = 2,
                        Foreground = Brush.Parse("#C77E68")
                    },
                    new TextBlock
                    {
                        Text = $"Run the {plan.ProfileName} diagnostic?",
                        FontSize = 27,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = $"This profile may transfer up to {FormatBytes(plan.TransferCapBytes)}. Avoid metered or cellular connections. The app asks again automatically if a future version raises this ceiling.",
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 22,
                        Foreground = Brush.Parse("#BDB8B0")
                    },
                    new Border
                    {
                        Background = Brush.Parse("#292317"),
                        BorderBrush = Brush.Parse("#463A25"),
                        BorderThickness = new Avalonia.Thickness(1),
                        CornerRadius = new Avalonia.CornerRadius(12),
                        Padding = new Avalonia.Thickness(14),
                        Child = new StackPanel
                        {
                            Spacing = 5,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "TRANSFER PLAN",
                                    FontSize = 10,
                                    FontWeight = FontWeight.SemiBold,
                                    LetterSpacing = 1.5,
                                    Foreground = Brush.Parse("#C77E68")
                                },
                                new TextBlock
                                {
                                    Text = $"{plan.DownloadStages.Count} download stages · {plan.UploadStages.Count} upload stages · about {plan.EstimatedSeconds} seconds",
                                    TextWrapping = TextWrapping.Wrap,
                                    Foreground = Brush.Parse("#D8D3CB")
                                }
                            }
                        }
                    },
                    rememberChoice,
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
}
