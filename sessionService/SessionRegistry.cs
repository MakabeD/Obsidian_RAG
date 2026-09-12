using System.Collections.Concurrent;
using configuration;
using Microsoft.Extensions.Options;

public class SessionRegistry
{
    private readonly ConcurrentDictionary<string, DateTime> _lastSeen = new();
    private readonly PriorityQueue<string, DateTime> _expiry = new();
    private readonly object _lock = new();
    private readonly TimeSpan _ttl;
    private readonly TimeProvider _time;

    public SessionRegistry(IOptions<RagOptions> options, TimeProvider? timeProvider = null)
    {
        _ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.SessionTtlMinutes));
        _time = timeProvider ?? TimeProvider.System;
    }

    public string Create()
    {
        string id = Guid.NewGuid().ToString("N");
        DateTime now = UtcNow();
        _lastSeen[id] = now;
        Schedule(id, now);
        return id;
    }

    public bool Exists(string sessionId) => _lastSeen.ContainsKey(sessionId);

    public void Touch(string sessionId)
    {
        DateTime now = UtcNow();
        _lastSeen[sessionId] = now;
        Schedule(sessionId, now);
    }

    public bool Remove(string sessionId) => _lastSeen.TryRemove(sessionId, out _);

    public List<string> PopExpired()
    {
        DateTime now = UtcNow();
        List<string> expired = new();
        lock (_lock)
        {
            while (_expiry.TryPeek(out _, out DateTime expiryAt) && expiryAt <= now)
            {
                string id = _expiry.Dequeue();
                if (_lastSeen.TryGetValue(id, out DateTime lastSeen) && now - lastSeen >= _ttl)
                {
                    if (_lastSeen.TryRemove(id, out _))
                        expired.Add(id);
                }
            }
        }
        return expired;
    }

    public void Reinstate(string sessionId)
    {
        DateTime now = UtcNow();
        _lastSeen[sessionId] = now;
        Schedule(sessionId, now);
    }

    private void Schedule(string sessionId, DateTime now)
    {
        lock (_lock)
        {
            _expiry.Enqueue(sessionId, now + _ttl);
        }
    }

    private DateTime UtcNow() => _time.GetUtcNow().UtcDateTime;
}