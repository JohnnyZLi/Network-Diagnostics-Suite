using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class CommandPalette : UserControl
{
    private IReadOnlyList<WorkbenchCommand> commands = [];
    private IReadOnlyList<WorkbenchCommand> filtered = [];
    private int selectedIndex;

    public CommandPalette()
    {
        InitializeComponent();
    }

    public event EventHandler<CommandInvokedEventArgs>? CommandInvoked;

    public bool IsOpen => IsVisible;

    public void Open(IReadOnlyList<WorkbenchCommand> availableCommands)
    {
        commands = availableCommands;
        SearchBox.Text = string.Empty;
        selectedIndex = 0;
        IsVisible = true;
        RefreshResults();
        Dispatcher.UIThread.Post(() => SearchBox.Focus());
    }

    public void Close()
    {
        IsVisible = false;
        SearchBox.Text = string.Empty;
        commands = [];
        filtered = [];
        ResultsPanel.Children.Clear();
    }

    private void SearchChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        selectedIndex = 0;
        RefreshResults();
    }

    private void SearchKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        switch (eventArgs.Key)
        {
            case Key.Escape:
                Close();
                eventArgs.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                eventArgs.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                eventArgs.Handled = true;
                break;
            case Key.Enter:
                InvokeSelected();
                eventArgs.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (filtered.Count == 0) return;
        selectedIndex = (selectedIndex + delta + filtered.Count) % filtered.Count;
        RenderRows();
    }

    private void InvokeSelected()
    {
        if (filtered.Count == 0) return;
        Invoke(filtered[Math.Clamp(selectedIndex, 0, filtered.Count - 1)]);
    }

    private void CommandClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: WorkbenchCommand command })
        {
            Invoke(command);
        }
    }

    private void Invoke(WorkbenchCommand command)
    {
        if (!command.Enabled) return;
        Close();
        CommandInvoked?.Invoke(this, new CommandInvokedEventArgs(command));
    }

    private void RefreshResults()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        filtered = commands
            .Select(command => new { Command = command, Score = Score(command, query) })
            .Where(item => item.Score < int.MaxValue)
            .OrderBy(item => item.Score)
            .ThenBy(item => item.Command.Title, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Command)
            .Take(14)
            .ToArray();
        selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, filtered.Count - 1));
        RenderRows();
    }

    private void RenderRows()
    {
        ResultsPanel.Children.Clear();
        ResultCountText.Text = filtered.Count == 1 ? "1 command" : $"{filtered.Count} commands";

        if (filtered.Count == 0)
        {
            var empty = new StackPanel { Margin = new Avalonia.Thickness(12, 18), Spacing = 5 };
            empty.Children.Add(new TextBlock
            {
                Text = "No matching command",
                FontSize = 14,
                FontWeight = FontWeight.SemiBold
            });
            var detail = new TextBlock
            {
                Text = "Try a workspace, action, setting, report, or diagnostic term.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            detail.Classes.Add("muted");
            empty.Children.Add(detail);
            ResultsPanel.Children.Add(empty);
            return;
        }

        for (var index = 0; index < filtered.Count; index++)
        {
            var command = filtered[index];
            var title = new TextBlock
            {
                Text = command.Title,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var detail = new TextBlock
            {
                Text = command.Detail,
                FontSize = 10,
                Foreground = Brush.Parse("#969B9B"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var copy = new StackPanel { Spacing = 3 };
            copy.Children.Add(title);
            copy.Children.Add(detail);

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 14 };
            grid.Children.Add(copy);
            if (!string.IsNullOrWhiteSpace(command.Shortcut))
            {
                var shortcut = new Border
                {
                    Background = Brush.Parse("#111415"),
                    BorderBrush = Brush.Parse("#3B4142"),
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(5),
                    Padding = new Avalonia.Thickness(7, 3),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = command.Shortcut,
                        FontSize = 9,
                        Foreground = Brush.Parse("#A8ADAD")
                    }
                };
                Grid.SetColumn(shortcut, 1);
                grid.Children.Add(shortcut);
            }

            var button = new Button
            {
                Content = grid,
                Tag = command,
                IsEnabled = command.Enabled
            };
            button.Classes.Add("command");
            if (index == selectedIndex) button.Classes.Add("selected");
            button.Click += CommandClicked;
            ResultsPanel.Children.Add(button);
        }
    }

    private static int Score(WorkbenchCommand command, string query)
    {
        if (query.Length == 0) return command.Priority;
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var haystack = $"{command.Title} {command.Detail} {command.Keywords}";
        if (terms.Any(term => !haystack.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return int.MaxValue;
        }
        if (command.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (command.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) return 10;
        if (command.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase)) return 20;
        return 30 + command.Priority;
    }
}

public sealed record WorkbenchCommand(
    string Id,
    string Title,
    string Detail,
    string Keywords = "",
    string? Shortcut = null,
    bool Enabled = true,
    int Priority = 50);

public sealed class CommandInvokedEventArgs(WorkbenchCommand command) : EventArgs
{
    public WorkbenchCommand Command { get; } = command;
}
