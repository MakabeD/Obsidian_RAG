using System.Collections.Concurrent;
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
        Assert.True(_registry.TryCreate(out string id));

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
        Assert.True(_registry.TryCreate(out string id));

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
        Assert.True(_registry.TryCreate(out string id));

        _registry.Remove(id);
        _time.UtcNowValue = Base.AddMinutes(11);

        Assert.Empty(_registry.PopExpired());
        Assert.False(_registry.Exists(id));
    }

    [Fact]
    public void A_session_expires_at_exactly_the_ttl_boundary()
    {
        Assert.True(_registry.TryCreate(out string id));

_time.UtcNowValue = Base.AddMinutes(10);
        Assert.Equal([id], _registry.PopExpired());
    }

    [Fact]
    public void Reinstate_puts_an_expired_session_back_until_ttl_elapses_again()
    {
        Assert.True(_registry.TryCreate(out string id));

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
    public void Create_beyond_the_cap_is_rejected()
    {
        RagOptions options = new() { SessionTtlMinutes = 10, MaxConcurrentSessions = 3 };
        SessionRegistry registry = new(Options.Create(options), _time);

        Assert.True(registry.TryCreate(out string first));
        Assert.True(registry.TryCreate(out string second));
        Assert.True(registry.TryCreate(out string third));
        Assert.False(registry.TryCreate(out string fourth));
        Assert.Equal(string.Empty, fourth);

        Assert.True(registry.Exists(first));
        Assert.True(registry.Exists(second));
        Assert.True(registry.Exists(third));
        Assert.False(registry.Exists(fourth));
    }

    [Fact]
    public void A_removed_session_frees_a_slot_for_a_new_one()
    {
        RagOptions options = new() { SessionTtlMinutes = 10, MaxConcurrentSessions = 2 };
        SessionRegistry registry = new(Options.Create(options), _time);

        Assert.True(registry.TryCreate(out string first));
        Assert.True(registry.TryCreate(out string second));
        Assert.False(registry.TryCreate(out _));

        Assert.True(registry.Remove(first));

        Assert.True(registry.TryCreate(out string third));
        Assert.True(registry.Exists(third));
        Assert.False(registry.Exists(first));
    }

    [Fact]
    public void Rejected_creates_leave_nothing_behind()
    {
        RagOptions options = new() { SessionTtlMinutes = 10, MaxConcurrentSessions = 2 };
        SessionRegistry registry = new(Options.Create(options), _time);

        Assert.True(registry.TryCreate(out _));
        Assert.True(registry.TryCreate(out _));
        for (int i = 0; i < 5; i++)
        {
            Assert.False(registry.TryCreate(out _));
        }

        _time.UtcNowValue = Base.AddMinutes(11);

        List<string> expired = registry.PopExpired();
        Assert.Equal(2, expired.Count);
        Assert.Equal(2, expired.Distinct().Count());
        Assert.Empty(registry.PopExpired());
    }

    [Fact]
    public void A_parallel_flood_never_exceeds_the_cap()
    {
        const int cap = 50;
        const int attempts = 2000;
        RagOptions options = new() { SessionTtlMinutes = 10, MaxConcurrentSessions = cap };
        SessionRegistry registry = new(Options.Create(options), _time);

        ConcurrentBag<string> created = new();
        Parallel.For(0, attempts, _ =>
        {
            if (registry.TryCreate(out string id))
            {
                created.Add(id);
            }
        });

        Assert.Equal(cap, created.Count);
        Assert.Equal(cap, created.Distinct().Count());

        _time.UtcNowValue = Base.AddMinutes(11);
        Assert.Equal(cap, registry.PopExpired().Count);
    }

    [Fact]
    public void Concurrent_create_touch_and_pop_keep_the_registry_consistent()
    {
        const int sessions = 200;
        List<string> ids = Enumerable.Range(0, sessions)
            .AsParallel()
            .Select(_ =>
            {
                Assert.True(_registry.TryCreate(out string id));
                return id;
            })
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