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
        Background = Brush.Parse("#F5F2EC");

        rememberChoice = new CheckBox
        {
            Content = $"Remember this choice for the {plan.ProfileName} profile on this computer.",
            IsChecked = true
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(16, 9),
            MinWidth = 96
        };
        cancel.Click += (_, _) => Close(new DataUseConfirmation(false, false));
        var confirm = new Button
        {
            Content = $"Run {plan.ProfileName.ToLowerInvariant()} test",
            Padding = new Avalonia.Thickness(16, 9),
            MinWidth = 150,
            Background = Brush.Parse("#A4553B"),
            Foreground = Brushes.White
        };
        confirm.Click += (_, _) => Close(new DataUseConfirmation(true, rememberChoice.IsChecked == true));

        Content = new Border
        {
            Padding = new Avalonia.Thickness(24),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = "CONFIRM DATA USE",
                        FontSize = 11,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brush.Parse("#A4553B")
                    },
                    new TextBlock
                    {
                        Text = $"Run the {plan.ProfileName} test?",
                        FontSize = 25,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = $"This diagnostic may transfer up to {FormatBytes(plan.TransferCapBytes)}. The selected {plan.Method.ToString().ToLowerInvariant()} method determines which stages run. Avoid metered or cellular connections.",
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 21
                    },
                    rememberChoice,
                    new TextBlock
                    {
                        Text = "You will be asked again if this profile's transfer cap increases.",
                        Foreground = Brush.Parse("#68645E"),
                        FontSize = 12,
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
}
