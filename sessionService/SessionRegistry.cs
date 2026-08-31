using System.Collections.Concurrent;
using configuration;
using Microsoft.Extensions.Options;

public class SessionRegistry
{
    private readonly ConcurrentDictionary<string, DateTime> _lastSeen = new();
    private readonly TimeSpan _ttl;

    public SessionRegistry(IOptions<RagOptions> options)
    {
        _ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.SessionTtlMinutes));
    }

    public string Create()
    {
        string id = Guid.NewGuid().ToString("N");
        _lastSeen[id] = DateTime.UtcNow;
        return id;
    }

    public bool Exists(string sessionId) => _lastSeen.ContainsKey(sessionId);

    public void Touch(string sessionId)
    {
        _lastSeen[sessionId] = DateTime.UtcNow;
    }

    public bool Remove(string sessionId) => _lastSeen.TryRemove(sessionId, out _);

    public List<string> GetExpired()
    {
        DateTime now = DateTime.UtcNow;
        return _lastSeen
            .Where(kvp => now - kvp.Value > _ttl)
            .Select(kvp => kvp.Key)
            .ToList();
    }
}
