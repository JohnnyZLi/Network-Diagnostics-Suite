namespace NetworkDeepProbe.Diagnostics;

internal sealed class ConcurrentByteBudget
{
    private readonly object gate = new();
    private readonly long limit;
    private long consumed;
    private long reserved;

    public ConcurrentByteBudget(long limit)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        this.limit = limit;
    }

    public long Limit => limit;

    public long Consumed
    {
        get
        {
            lock (gate) return consumed;
        }
    }

    public bool IsExhausted
    {
        get
        {
            lock (gate) return consumed >= limit;
        }
    }

    public int Reserve(int maximumBytes)
    {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        lock (gate)
        {
            var available = limit - consumed - reserved;
            if (available <= 0) return 0;
            var granted = (int)Math.Min(maximumBytes, available);
            reserved += granted;
            return granted;
        }
    }

    public bool Commit(int reservedBytes, int consumedBytes)
    {
        if (reservedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(reservedBytes));
        if (consumedBytes < 0 || consumedBytes > reservedBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(consumedBytes));
        }

        lock (gate)
        {
            if (reservedBytes > reserved) throw new InvalidOperationException("The committed reservation exceeds the outstanding byte budget.");
            reserved -= reservedBytes;
            consumed += consumedBytes;
            if (consumed > limit) throw new InvalidOperationException("The committed payload exceeded the byte budget.");
            return consumed >= limit;
        }
    }

    public void Release(int reservedBytes)
    {
        if (reservedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(reservedBytes));
        lock (gate)
        {
            if (reservedBytes > reserved) throw new InvalidOperationException("The released reservation exceeds the outstanding byte budget.");
            reserved -= reservedBytes;
        }
    }
}
