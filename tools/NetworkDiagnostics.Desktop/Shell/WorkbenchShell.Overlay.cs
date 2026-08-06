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
    private Border? overlayHeader;
    private ContentControl? overlayHost;
    private TextBlock? overlayTitle;
    private double? overlayRequestedMaxHeight;

    public event EventHandler? OverlayCloseRequested;

    public bool OverlayOpen => overlayRoot?.IsVisible == true && overlayRoot.IsHitTestVisible;

    public bool IsOverlayContent(Control? content) =>
        content is not null && ReferenceEquals(overlayHost?.Content, content);

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

        overlayHeader = new Border { Child = headerGrid };
        overlayHeader.Classes.Add("modalHeader");

        overlayHost = new ContentControl
        {
            Name = "OverlayHost",
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };

        var sheetGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        sheetGrid.Children.Add(overlayHeader);
        Grid.SetRow(overlayHost, 1);
        sheetGrid.Children.Add(overlayHost);

        overlaySheet = new Border
        {
            Name = "OverlaySheet",
            Margin = new Thickness(30),
            MaxWidth = 1180,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
            Opacity = 1,
            Child = sheetGrid
        };
        overlaySheet.Classes.Add("modalSheet");

        overlayRoot = new Grid
        {
            Name = "OverlayRoot",
            IsVisible = false,
            IsHitTestVisible = false,
            Opacity = 1,
            ZIndex = 50
        };
        overlayRoot.Children.Add(backdrop);
        overlayRoot.Children.Add(overlaySheet);
        ShellGrid.Children.Add(overlayRoot);
        ShellGrid.SizeChanged += OverlayViewportChanged;
    }

    public void SetReducedMotion(bool value)
    {
        // Overlay presentation is intentionally atomic for every motion preference.
        // Keeping this API preserves the accessibility call site without maintaining
        // a second transition path that can flash on platform compositors.
    }

    public void OpenOverlay(
        string title,
        Control content,
        double maxWidth = 1180,
        double? maxHeight = null,
        bool stretchWidth = false,
        bool showHeader = true)
    {
        EnsureOverlay();

        if (overlayHost!.Content is Control previous && !ReferenceEquals(previous, content))
        {
            overlayHost.Content = null;
            previous.IsVisible = false;
        }

        DetachFromLogicalParent(content);
        content.IsVisible = true;
        overlayTitle!.Text = title;
        overlayHeader!.IsVisible = showHeader;
        overlaySheet!.MaxWidth = maxWidth;
        overlaySheet.HorizontalAlignment = stretchWidth
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        overlayRequestedMaxHeight = maxHeight;
        overlayHost.Content = content;
        ApplyOverlayBounds();

        // Mount the completely laid-out sheet in one visual state. Opacity ramps on
        // large desktop surfaces caused visible flashes on macOS even when the
        // backdrop itself was not animated.
        overlaySheet.Opacity = 1;
        overlayRoot!.Opacity = 1;
        overlayRoot.IsVisible = true;
        overlayRoot.IsHitTestVisible = true;
        inspectorRequested = false;
        RefreshResponsiveChrome();
    }

    public void SelectControlCenter()
    {
        currentWorkspace = WorkspaceKind.Test;
        RenderProductContext();
        RefreshResponsiveChrome();
    }

    public void CloseOverlay()
    {
        if (overlayRoot is null || overlayHost is null || !OverlayOpen) return;

        // Close atomically as well. There is no transparent intermediate frame where
        // the backdrop remains while the report body has already disappeared.
        overlayRoot.IsHitTestVisible = false;
        var content = overlayHost.Content as Control;
        overlayHost.Content = null;
        if (content is not null) content.IsVisible = false;
        overlayRoot.IsVisible = false;
        overlayRoot.Opacity = 1;
        if (overlaySheet is not null) overlaySheet.Opacity = 1;
        RefreshResponsiveChrome();
    }

    private void OverlayViewportChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyOverlayBounds();

    private void ApplyOverlayBounds()
    {
        if (overlaySheet is null) return;

        var width = Math.Max(320, ShellGrid.Bounds.Width);
        var height = Math.Max(320, ShellGrid.Bounds.Height);
        var margin = width < 900 ? 12d : 30d;
        var availableHeight = Math.Max(280, height - margin * 2);
        var resolvedHeight = overlayRequestedMaxHeight is { } requested
            ? Math.Min(requested, availableHeight)
            : availableHeight;

        overlaySheet.Margin = new Thickness(margin);
        overlaySheet.Height = resolvedHeight;
        overlaySheet.MaxHeight = resolvedHeight;
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
