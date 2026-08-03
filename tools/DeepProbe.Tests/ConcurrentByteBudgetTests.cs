using NetworkDeepProbe.Diagnostics;

namespace NetworkDeepProbe.Tests;

public sealed class ConcurrentByteBudgetTests
{
    [Fact]
    public async Task ConcurrentReservationsNeverCommitPastTheLimit()
    {
        const int limit = 1_000_003;
        var budget = new ConcurrentByteBudget(limit);
        var workers = Enumerable.Range(0, 12)
            .Select(async worker =>
            {
                while (!budget.IsExhausted)
                {
                    var reservation = budget.Reserve(64 * 1024);
                    if (reservation == 0)
                    {
                        await Task.Yield();
                        continue;
                    }

                    await Task.Delay(worker % 3);
                    budget.Commit(reservation, reservation);
                }
            });

        await Task.WhenAll(workers);

        Assert.Equal(limit, budget.Consumed);
        Assert.True(budget.IsExhausted);
    }

    [Fact]
    public void UnusedReservationIsReturnedToTheBudget()
    {
        var budget = new ConcurrentByteBudget(100);
        var first = budget.Reserve(80);

        budget.Commit(first, 30);
        var second = budget.Reserve(100);

        Assert.Equal(70, second);
        Assert.True(budget.Commit(second, 70));
        Assert.Equal(100, budget.Consumed);
    }

    [Fact]
    public void ReleasedReservationCanBeClaimedAgain()
    {
        var budget = new ConcurrentByteBudget(100);
        var first = budget.Reserve(100);

        budget.Release(first);
        var second = budget.Reserve(100);

        Assert.Equal(100, second);
        Assert.Equal(0, budget.Consumed);
    }
}
