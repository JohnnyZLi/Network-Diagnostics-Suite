namespace NetworkDiagnostics.Desktop.Navigation;

public sealed class NavigationService
{
    private readonly Stack<NavigationEntry> backStack = new();
    private readonly Stack<NavigationEntry> forwardStack = new();

    public event EventHandler<NavigationChangedEventArgs>? Changed;

    public NavigationEntry? Current { get; private set; }

    public bool CanGoBack => backStack.Count > 0;

    public bool CanGoForward => forwardStack.Count > 0;

    public int BackCount => backStack.Count;

    public int ForwardCount => forwardStack.Count;

    public void Initialize(
        AppDestination destination,
        NavigationViewState? viewState = null)
    {
        backStack.Clear();
        forwardStack.Clear();
        Current = new NavigationEntry(destination, viewState ?? new NavigationViewState());
        RaiseChanged();
    }

    public void Navigate(
        AppDestination destination,
        NavigationViewState? viewState = null,
        bool replaceCurrent = false)
    {
        var next = new NavigationEntry(destination, viewState ?? new NavigationViewState());

        if (Current is null)
        {
            Current = next;
            forwardStack.Clear();
            RaiseChanged();
            return;
        }

        if (replaceCurrent)
        {
            Current = next;
            forwardStack.Clear();
            RaiseChanged();
            return;
        }

        if (Current.Destination == destination && Current.ViewState == next.ViewState)
        {
            return;
        }

        backStack.Push(Current);
        Current = next;
        forwardStack.Clear();
        RaiseChanged();
    }

    public void UpdateCurrentState(NavigationViewState viewState)
    {
        if (Current is null) return;
        Current = Current with { ViewState = viewState };
    }

    public bool GoBack()
    {
        if (Current is null || backStack.Count == 0) return false;

        forwardStack.Push(Current);
        Current = backStack.Pop();
        RaiseChanged();
        return true;
    }

    public bool GoForward()
    {
        if (Current is null || forwardStack.Count == 0) return false;

        backStack.Push(Current);
        Current = forwardStack.Pop();
        RaiseChanged();
        return true;
    }

    public void ClearForwardHistory() => forwardStack.Clear();

    private void RaiseChanged()
    {
        if (Current is not null)
        {
            Changed?.Invoke(this, new NavigationChangedEventArgs(Current));
        }
    }
}
