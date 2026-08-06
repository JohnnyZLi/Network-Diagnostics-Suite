using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class WorkbenchShell
{
    private Grid? overlayRoot;
    private Border? overlaySheet;
    private ContentControl? overlayHost;
    private TextBlock? overlayTitle;

    public event EventHandler? OverlayCloseRequested;

    public bool OverlayOpen => overlayRoot?.IsVisible == true;

    public void EnsureOverlay()
    {
        if (overlayRoot is not null) return;

        var backdrop = new Border();
        backdrop.Classes.Add("modalBackdrop");
        backdrop.PointerPressed += OverlayBackdropPressed;

        overlayTitle = new TextBlock
        {
            FontSize = 16,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var close = new Button
        {
            Content = "×",
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        close.Classes.Add("ghost");
        close.Click += OverlayCloseClicked;

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12
        };
        headerGrid.Children.Add(overlayTitle);
        Grid.SetColumn(close, 1);
        headerGrid.Children.Add(close);

        var header = new Border { Child = headerGrid };
        header.Classes.Add("modalHeader");

        overlayHost = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };

        var sheetGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        sheetGrid.Children.Add(header);
        Grid.SetRow(overlayHost, 1);
        sheetGrid.Children.Add(overlayHost);

        overlaySheet = new Border
        {
            Margin = new Thickness(30),
            MaxWidth = 1180,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true,
            Child = sheetGrid
        };
        overlaySheet.Classes.Add("modalSheet");

        overlayRoot = new Grid
        {
            IsVisible = false,
            ZIndex = 50
        };
        overlayRoot.Children.Add(backdrop);
        overlayRoot.Children.Add(overlaySheet);
        ShellGrid.Children.Add(overlayRoot);
    }

    public void OpenOverlay(
        string title,
        Control content,
        double maxWidth = 1180,
        double? maxHeight = null,
        bool stretchWidth = false)
    {
        EnsureOverlay();
        DetachFromLogicalParent(content);
        content.IsVisible = true;
        overlayTitle!.Text = title;
        overlaySheet!.MaxWidth = maxWidth;
        overlaySheet.HorizontalAlignment = stretchWidth
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        overlaySheet.MaxHeight = maxHeight ?? double.PositiveInfinity;
        overlaySheet.VerticalAlignment = maxHeight is null
            ? VerticalAlignment.Stretch
            : VerticalAlignment.Center;
        overlayHost!.Content = content;
        overlayRoot!.IsVisible = true;
        inspectorRequested = false;
        RefreshResponsiveChrome();
    }

    public void SelectControlCenter()
    {
        currentWorkspace = WorkspaceKind.Test;
        SelectWorkspace(WorkspaceKind.Test);
        RenderProductContext();
        RefreshResponsiveChrome();
    }

    public void CloseOverlay()
    {
        if (overlayRoot is not null) overlayRoot.IsVisible = false;
        RefreshResponsiveChrome();
    }

    private void OverlayCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) =>
        OverlayCloseRequested?.Invoke(this, EventArgs.Empty);

    private void OverlayBackdropPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.Source == sender)
        {
            OverlayCloseRequested?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
        }
    }

    private static void DetachFromLogicalParent(Control content)
    {
        switch (content.GetLogicalParent())
        {
            case Panel panel:
                panel.Children.Remove(content);
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, content):
                contentControl.Content = null;
                break;
        }
    }
}
