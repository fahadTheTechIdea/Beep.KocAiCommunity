using Beep.KocAiCommunity.Application.Experiments;
using Beep.KocAiCommunity.Infrastructure.Experiments;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class BoundedMetricChannelTests
{
    private static RunMetricEntry M(int step) => new("Accuracy", 0.5 + (step * 0.01), "validation", "trial", step);

    [Fact]
    public void TryPublish_never_blocks_and_drops_when_full()
    {
        var channel = new BoundedMetricChannel(capacity: 2);

        // No reader is draining, so only `capacity` items are accepted; the rest are refused (never block).
        channel.TryPublish(M(1)).Should().BeTrue();
        channel.TryPublish(M(2)).Should().BeTrue();
        channel.TryPublish(M(3)).Should().BeFalse();
    }

    [Fact]
    public async Task Drain_delivers_all_accepted_items_in_batches()
    {
        var channel = new BoundedMetricChannel(capacity: 16);
        for (var i = 1; i <= 5; i++)
        {
            channel.TryPublish(M(i)).Should().BeTrue();
        }

        channel.Complete();

        var batches = new List<int>();
        var received = new List<RunMetricEntry>();
        await channel.DrainAsync(batch =>
        {
            batches.Add(batch.Count);
            received.AddRange(batch);
            return Task.CompletedTask;
        }, batchSize: 2);

        received.Should().HaveCount(5);
        received.Select(m => m.Step).Should().Equal(1, 2, 3, 4, 5);
        batches.Should().Equal(2, 2, 1); // batched, with a final partial flush
    }

    [Fact]
    public async Task Drain_completes_when_the_channel_is_completed_empty()
    {
        var channel = new BoundedMetricChannel();
        channel.Complete();

        var calls = 0;
        await channel.DrainAsync(_ => { calls++; return Task.CompletedTask; });

        calls.Should().Be(0);
    }
}
