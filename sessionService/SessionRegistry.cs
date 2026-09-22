using System.Collections.Concurrent;
using configuration;
using Microsoft.Extensions.Options;

public class SessionRegistry
{
    private readonly ConcurrentDictionary<string, DateTime> _lastSeen = new();
    private readonly HashSet<string> _expiring = new();
    private readonly HashSet<string> _deleting = new();
    private readonly PriorityQueue<string, DateTime> _expiry = new();
    private readonly object _lock = new();
    private readonly TimeSpan _ttl;
    private readonly int _maxSessions;
    private readonly TimeProvider _time;

    public SessionRegistry(IOptions<RagOptions> options, TimeProvider? timeProvider = null)
    {
        _ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.SessionTtlMinutes));
        _maxSessions = Math.Max(1, options.Value.MaxConcurrentSessions);
        _time = timeProvider ?? TimeProvider.System;
    }

    public bool TryCreate(out string sessionId)
    {
        string id = Guid.NewGuid().ToString("N");
        DateTime now = UtcNow();
        lock (_lock)
        {
            if (_lastSeen.Count >= _maxSessions)
            {
                sessionId = string.Empty;
                return false;
            }

            _lastSeen[id] = now;
            Schedule(id, now);
        }

        sessionId = id;
        return true;
    }

    public bool Exists(string sessionId) => _lastSeen.ContainsKey(sessionId);

    public virtual bool Touch(string sessionId)
    {
        DateTime now = UtcNow();
        lock (_lock)
        {
            if (_lastSeen.ContainsKey(sessionId))
            {
                _lastSeen[sessionId] = now;
                Schedule(sessionId, now);
                return true;
            }

            if (_expiring.Remove(sessionId))
            {
                _lastSeen[sessionId] = now;
                Schedule(sessionId, now);
                return true;
            }

            return false;
        }
    }

    public bool Remove(string sessionId)
    {
        lock (_lock)
        {
            bool removed = _lastSeen.TryRemove(sessionId, out _);
            _expiring.Remove(sessionId);
            _deleting.Remove(sessionId);
            return removed;
        }
    }

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
                    _lastSeen.TryRemove(id, out _);
                    _expiring.Add(id);
                    expired.Add(id);
                }
            }
        }
        return expired;
    }

    public bool TryClaimExpired(string sessionId)
    {
        lock (_lock)
        {
            if (_expiring.Remove(sessionId))
            {
                _deleting.Add(sessionId);
                return true;
            }

            return false;
        }
    }

    public void ConfirmDelete(string sessionId)
    {
        lock (_lock)
        {
            _deleting.Remove(sessionId);
        }
    }

    public void FailDelete(string sessionId)
    {
        DateTime now = UtcNow();
        lock (_lock)
        {
            if (_deleting.Remove(sessionId))
            {
                _lastSeen[sessionId] = now;
                Schedule(sessionId, now);
            }
        }
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