using configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ObsidianRAG.Tests.sessionService;

public class SessionRegistryTests
{
    private static readonly DateTimeOffset Base = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNowValue { get; set; }
        public override DateTimeOffset GetUtcNow() => UtcNowValue;
    }

    private readonly FakeTimeProvider _time = new() { UtcNowValue = Base };
    private readonly SessionRegistry _registry;

    public SessionRegistryTests()
    {
        RagOptions options = new() { SessionTtlMinutes = 10 };
        _registry = new SessionRegistry(
            Options.Create(options),
            _time);
    }

    [Fact]
    public void PopExpired_returns_sessions_whose_ttl_has_elapsed_exactly_once()
    {
        string id = _registry.Create();

        _time.UtcNowValue = Base.AddMinutes(9);
        Assert.Empty(_registry.PopExpired());
        Assert.True(_registry.Exists(id));

        _time.UtcNowValue = Base.AddMinutes(11);
        List<string> expired = _registry.PopExpired();

        Assert.Equal([id], expired);
        Assert.False(_registry.Exists(id));
        Assert.Empty(_registry.PopExpired());
    }

    [Fact]
    public void Touching_a_session_extends_its_expiry()
    {
        string id = _registry.Create();

        _time.UtcNowValue = Base.AddMinutes(9);
        _registry.Touch(id);

        _time.UtcNowValue = Base.AddMinutes(11);
        Assert.Empty(_registry.PopExpired());
        Assert.True(_registry.Exists(id));

        _time.UtcNowValue = Base.AddMinutes(18).AddSeconds(59);
        Assert.Empty(_registry.PopExpired());

        _time.UtcNowValue = Base.AddMinutes(20);
        Assert.Equal([id], _registry.PopExpired());
        Assert.False(_registry.Exists(id));
    }

    [Fact]
    public void PopExpired_does_not_return_sessions_removed_before_expiry()
    {
        string id = _registry.Create();

        _registry.Remove(id);
        _time.UtcNowValue = Base.AddMinutes(11);

        Assert.Empty(_registry.PopExpired());
        Assert.False(_registry.Exists(id));
    }

    [Fact]
    public void A_session_expires_at_exactly_the_ttl_boundary()
    {
        string id = _registry.Create();

_time.UtcNowValue = Base.AddMinutes(10);
        Assert.Equal([id], _registry.PopExpired());
    }

    [Fact]
    public void Reinstate_puts_an_expired_session_back_until_ttl_elapses_again()
    {
        string id = _registry.Create();

        _time.UtcNowValue = Base.AddMinutes(11);
        Assert.Equal([id], _registry.PopExpired());

        _registry.Reinstate(id);

        _time.UtcNowValue = Base.AddMinutes(12);
        Assert.True(_registry.Exists(id));
        Assert.Empty(_registry.PopExpired());

        _time.UtcNowValue = Base.AddMinutes(22);
        Assert.Equal([id], _registry.PopExpired());
    }

    [Fact]
    public void Concurrent_create_touch_and_pop_keep_the_registry_consistent()
    {
        const int sessions = 200;
        List<string> ids = Enumerable.Range(0, sessions)
            .AsParallel()
            .Select(_ => _registry.Create())
            .ToList();

        Parallel.For(0, sessions, i => _registry.Touch(ids[i]));

        _time.UtcNowValue = Base.AddMinutes(1);
        Assert.Empty(_registry.PopExpired());
        Assert.All(ids, id => Assert.True(_registry.Exists(id)));

        _time.UtcNowValue = Base.AddMinutes(11);
        List<string> expired = _registry.PopExpired();

        Assert.Equal(sessions, expired.Count);
        Assert.Equal(sessions, expired.Distinct().Count());
    }
}