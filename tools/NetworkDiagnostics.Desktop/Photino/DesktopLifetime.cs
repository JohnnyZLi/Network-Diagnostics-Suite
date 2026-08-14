using System.Runtime.InteropServices;

namespace NetworkDiagnostics.Desktop;

public sealed class DesktopLifetime : IDisposable
{
    private readonly Action closeWindow;
    private readonly List<PosixSignalRegistration> registrations = [];
    private int closeRequested;
    private bool disposed;

    public DesktopLifetime(Action closeWindow)
    {
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        Console.CancelKeyPress += ConsoleCancelKeyPress;
        if (!OperatingSystem.IsWindows())
        {
            registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGTERM, SignalReceived));
            registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGINT, SignalReceived));
            registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGHUP, SignalReceived));
        }
    }

    public bool ShutdownRequested => Volatile.Read(ref closeRequested) != 0;

    public void RequestShutdown()
    {
        if (Interlocked.Exchange(ref closeRequested, 1) != 0) return;
        _ = Task.Run(() =>
        {
            try
            {
                closeWindow();
            }
            catch (ObjectDisposedException)
            {
                // The user may close the window at the same time as the process signal.
            }
            catch (InvalidOperationException)
            {
                // The native window may already be unwinding its message loop.
            }
        });
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Console.CancelKeyPress -= ConsoleCancelKeyPress;
        foreach (var registration in registrations) registration.Dispose();
        registrations.Clear();
    }

    private void ConsoleCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        RequestShutdown();
    }

    private void SignalReceived(PosixSignalContext context)
    {
        context.Cancel = true;
        RequestShutdown();
    }
}
