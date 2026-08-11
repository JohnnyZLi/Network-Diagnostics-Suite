namespace NetworkDiagnostics.Desktop;

public sealed class DesktopSingleInstance : IDisposable
{
    private readonly FileStream lockStream;

    private DesktopSingleInstance(FileStream lockStream)
    {
        this.lockStream = lockStream;
    }

    public static DesktopSingleInstance? TryAcquire(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        Directory.CreateDirectory(rootDirectory);
        var path = Path.Combine(rootDirectory, "desktop-instance.lock");
        try
        {
            var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            stream.SetLength(0);
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(Environment.ProcessId);
            writer.Flush();
            stream.Flush(flushToDisk: true);
            return new DesktopSingleInstance(stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose() => lockStream.Dispose();
}
