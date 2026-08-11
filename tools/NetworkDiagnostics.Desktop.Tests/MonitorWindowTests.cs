using NetworkDiagnostics.Desktop.Monitoring;
using Xunit;

namespace NetworkDiagnostics.Desktop.Tests;

public sealed class MonitorWindowTests
{
    [Fact]
    public void SevenDayWindowRoundTripsThroughPersistedContract()
    {
        var window = MonitorWindowExtensions.Parse("7d");

        Assert.Equal(MonitorWindow.SevenDays, window);
        Assert.Equal("7d", window.ContractId());
        Assert.Equal(TimeSpan.FromDays(7), window.Duration());
    }

    [Fact]
    public void UnknownWindowStillFallsBackToTwentyFourHours()
    {
        Assert.Equal(MonitorWindow.TwentyFourHours, MonitorWindowExtensions.Parse("unknown"));
    }
}
