using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using NetworkDiagnostics.Desktop.Navigation;

namespace NetworkDiagnostics.Desktop.Shell;

public sealed partial class WorkbenchShell
{
    private static readonly TimeSpan OverlayFadeDuration = TimeSpan.FromMilliseconds(110);

    private Grid? overlayRoot;
    private Border? overlaySheet;
    private Border? overlayHeader;
    private ContentControl? overlayHost;
    private TextBlock? overlayTitle;
    private double? overlayRequestedMaxHeight;
    private bool reducedMotion;
    private int overlayTransitionGeneration;

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
            Opacity = 0,
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
        ConfigureOverlayTransitions();
    }

    public void SetReducedMotion(bool value)
    {
        reducedMotion = value;
        ConfigureOverlayTransitions();
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
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            SetReducedMotion(topLevel.Classes.Contains("reducedMotion"));
        }

        var wasOpen = OverlayOpen;
        var generation = ++overlayTransitionGeneration;
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

        // The backdrop appears at its final, intentionally soft opacity immediately.
        // Only the bounded sheet fades, avoiding whole-window luminance changes.
        overlayRoot!.IsVisible = true;
        overlayRoot.IsHitTestVisible = true;
        overlayRoot.Opacity = 1;
        overlaySheet.Opacity = wasOpen || reducedMotion ? 1 : 0;
        inspectorRequested = false;
        RefreshResponsiveChrome();

        if (!wasOpen && !reducedMotion)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (generation == overlayTransitionGeneration && OverlayOpen && overlaySheet is not null)
                {
                    overlaySheet.Opacity = 1;
                }
            }, DispatcherPriority.Render);
        }
    }

    public void SelectControlCenter()
    {
        currentWorkspace = WorkspaceKind.Test;
        RenderProductContext();
        RefreshResponsiveChrome();
    }

    public void CloseOverlay()
    {
        if (overlayRoot is null || overlaySheet is null || !OverlayOpen) return;
        var generation = ++overlayTransitionGeneration;
        overlayRoot.IsHitTestVisible = false;

        if (reducedMotion)
        {
            FinishOverlayClose(generation);
            return;
        }

        overlaySheet.Opacity = 0;
        DispatcherTimer.RunOnce(
            () => FinishOverlayClose(generation),
            OverlayFadeDuration + TimeSpan.FromMilliseconds(20));
    }

    private void FinishOverlayClose(int generation)
    {
        if (generation != overlayTransitionGeneration
            || overlayRoot is null
            || overlaySheet is null
            || overlayHost is null)
        {
            return;
        }

        var content = overlayHost.Content as Control;
        overlayHost.Content = null;
        if (content is not null) content.IsVisible = false;
        overlayRoot.IsVisible = false;
        overlayRoot.Opacity = 1;
        overlaySheet.Opacity = 0;
        RefreshResponsiveChrome();
    }

    private void ConfigureOverlayTransitions()
    {
        if (overlaySheet is null) return;
        overlaySheet.Transitions = reducedMotion
            ? null
            : new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = OverlayFadeDuration
                }
            };
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
