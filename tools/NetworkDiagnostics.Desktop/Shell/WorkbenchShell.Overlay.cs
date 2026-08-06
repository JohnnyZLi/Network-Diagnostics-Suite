using Avalonia;
using Avalonia.Controls;
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
    private Panel? focusedOriginalPanel;
    private ContentControl? focusedOriginalContentControl;
    private int focusedOriginalIndex = -1;

    public event EventHandler? OverlayCloseRequested;

    // Compatibility name for callers while the former modal surface now behaves as a
    // focused in-shell workspace. There is no backdrop or popup chrome.
    public bool OverlayOpen => overlayRoot?.IsVisible == true;

    public bool IsOverlayContent(Control? content) =>
        content is not null && ReferenceEquals(overlayHost?.Content, content);

    public void EnsureOverlay()
    {
        if (overlayRoot is not null) return;

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

        // Focused workspaces own their own title/action chrome. The compatibility
        // header stays mounted but hidden so older callers do not need a second path.
        overlayHeader = new Border
        {
            Child = headerGrid,
            IsVisible = false
        };
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
            Margin = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            ClipToBounds = true,
            Opacity = 1,
            Child = sheetGrid
        };
        // Retain the class for existing visual-test discovery. Local values above
        // neutralize the old rounded modal framing.
        overlaySheet.Classes.Add("modalSheet");

        overlayRoot = new Grid
        {
            Name = "OverlayRoot",
            IsVisible = false,
            IsHitTestVisible = false,
            Opacity = 1,
            ZIndex = 20
        };
        overlayRoot.Children.Add(overlaySheet);
        ShellGrid.Children.Add(overlayRoot);
        ShellGrid.SizeChanged += OverlayViewportChanged;
    }

    public void SetReducedMotion(bool value)
    {
        // Focused workspaces never animate, so reduced-motion needs no alternate path.
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
            RestoreFocusedContent(previous);
        }

        if (!ReferenceEquals(overlayHost.Content, content))
        {
            CaptureAndDetachFocusedContent(content);
            overlayHost.Content = content;
        }

        content.IsVisible = true;
        overlayTitle!.Text = title;
        overlayHeader!.IsVisible = false;

        // Reports and Settings are normal application workspaces. Fill the entire
        // content area below the persistent app header in one frame: no dimming,
        // backdrop, opacity ramp, centered card, or click-outside behavior.
        overlaySheet!.Margin = new Thickness(0);
        overlaySheet.HorizontalAlignment = HorizontalAlignment.Stretch;
        overlaySheet.VerticalAlignment = VerticalAlignment.Stretch;
        overlaySheet.Opacity = 1;
        overlaySheet.Transitions = null;
        ApplyOverlayBounds();

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

        overlayRoot.IsHitTestVisible = false;
        if (overlayHost.Content is Control content)
        {
            overlayHost.Content = null;
            content.IsVisible = false;
            RestoreFocusedContent(content);
        }

        overlayRoot.IsVisible = false;
        overlayRoot.Opacity = 1;
        if (overlaySheet is not null)
        {
            overlaySheet.Opacity = 1;
            overlaySheet.Transitions = null;
        }
        RefreshResponsiveChrome();
    }

    private void OverlayViewportChanged(object? sender, SizeChangedEventArgs eventArgs) =>
        ApplyOverlayBounds();

    private void ApplyOverlayBounds()
    {
        if (overlaySheet is null) return;

        var width = Math.Max(320, ShellGrid.Bounds.Width);
        var height = Math.Max(280, ShellGrid.Bounds.Height);
        overlaySheet.Width = width;
        overlaySheet.Height = height;
        overlaySheet.MaxWidth = width;
        overlaySheet.MaxHeight = height;
    }

    private void OverlayCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) =>
        OverlayCloseRequested?.Invoke(this, EventArgs.Empty);

    private void CaptureAndDetachFocusedContent(Control content)
    {
        focusedOriginalPanel = null;
        focusedOriginalContentControl = null;
        focusedOriginalIndex = -1;

        switch (content.GetLogicalParent())
        {
            case Panel panel:
                focusedOriginalPanel = panel;
                focusedOriginalIndex = panel.Children.IndexOf(content);
                panel.Children.Remove(content);
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, content):
                focusedOriginalContentControl = contentControl;
                contentControl.Content = null;
                break;
        }
    }

    private void RestoreFocusedContent(Control content)
    {
        if (focusedOriginalPanel is not null)
        {
            if (!focusedOriginalPanel.Children.Contains(content))
            {
                var index = Math.Clamp(focusedOriginalIndex, 0, focusedOriginalPanel.Children.Count);
                focusedOriginalPanel.Children.Insert(index, content);
            }
        }
        else if (focusedOriginalContentControl is not null
                 && focusedOriginalContentControl.Content is null)
        {
            focusedOriginalContentControl.Content = content;
        }

        focusedOriginalPanel = null;
        focusedOriginalContentControl = null;
        focusedOriginalIndex = -1;
    }
}
