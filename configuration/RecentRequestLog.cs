using Microsoft.Extensions.Options;

namespace configuration;

public sealed record RecentRequestEntry(
    DateTime Timestamp,
    string RequestId,
    string Method,
    string Path,
    int StatusCode,
    long ElapsedMs);

public sealed class RecentRequestLog
{
    private readonly RecentRequestEntry[] _buffer;
    private readonly object _lock = new();
    private int _head;
    private int _count;

    public RecentRequestLog(IOptions<RagOptions> options)
        : this(Math.Max(1, options.Value.RecentRequestCapacity)) { }

    internal RecentRequestLog(int capacity)
    {
        _buffer = new RecentRequestEntry[capacity];
    }

    public void Add(string requestId, string method, string path, int statusCode, long elapsedMs)
    {
        RecentRequestEntry entry = new(DateTime.UtcNow, requestId, method, path, statusCode, elapsedMs);
        lock (_lock)
        {
            _buffer[_head] = entry;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }
    }

    public IReadOnlyList<RecentRequestEntry> Snapshot()
    {
        lock (_lock)
        {
            RecentRequestEntry[] result = new RecentRequestEntry[_count];
            int start = _count < _buffer.Length ? 0 : _head;
            for (int i = 0; i < _count; i++)
                result[i] = _buffer[(start + i) % _buffer.Length];
            return result;
        }
    }
}
