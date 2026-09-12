using configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ObsidianRAG.Tests.configuration;

public class RecentRequestLogTests
{
    private static RecentRequestLog CreateLog(int capacity = 100) =>
        new(Options.Create(new RagOptions { RecentRequestCapacity = capacity }));

    [Fact]
    public void Snapshot_returns_recorded_requests_in_insertion_order()
    {
        RecentRequestLog log = CreateLog();

        log.Add("r1", "GET", "/a", 200, 5L);
        log.Add("r2", "POST", "/b", 404, 10L);

        IReadOnlyList<RecentRequestEntry> snapshot = log.Snapshot();

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("r1", snapshot[0].RequestId);
        Assert.Equal("GET", snapshot[0].Method);
        Assert.Equal("/a", snapshot[0].Path);
        Assert.Equal(200, snapshot[0].StatusCode);
        Assert.Equal(5L, snapshot[0].ElapsedMs);
        Assert.Equal("r2", snapshot[1].RequestId);
        Assert.Equal("POST", snapshot[1].Method);
        Assert.Equal("/b", snapshot[1].Path);
        Assert.Equal(404, snapshot[1].StatusCode);
        Assert.Equal(10L, snapshot[1].ElapsedMs);
    }

    [Fact]
    public void Adding_beyond_capacity_drops_the_oldest_entries()
    {
        RecentRequestLog log = CreateLog(capacity: 2);

        log.Add("r1", "GET", "/a", 200, 1L);
        log.Add("r2", "GET", "/b", 200, 2L);
        log.Add("r3", "GET", "/c", 500, 3L);

        IReadOnlyList<RecentRequestEntry> snapshot = log.Snapshot();

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("r2", snapshot[0].RequestId);
        Assert.Equal("r3", snapshot[1].RequestId);
    }
}
