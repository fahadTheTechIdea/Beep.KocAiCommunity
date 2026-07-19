using System.Threading.Channels;
using Beep.KocAiCommunity.Application.Experiments;

namespace Beep.KocAiCommunity.Infrastructure.Experiments;

/// <summary>
/// The non-blocking bridge between ML.NET's trial reporter and the database. Producers call
/// <see cref="TryPublish"/> (never blocks — returns false when the buffer is full, so training is
/// never slowed by a slow writer); a single background <see cref="DrainAsync"/> loop batches the
/// accepted metrics to a sink. This bounds run-metric cardinality by design.
/// </summary>
public sealed class BoundedMetricChannel
{
    private readonly Channel<RunMetricEntry> _channel;

    public BoundedMetricChannel(int capacity = 1024)
    {
        _channel = Channel.CreateBounded<RunMetricEntry>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait, // with TryWrite this makes a full channel return false, not block
            SingleReader = true,
        });
    }

    /// <summary>Enqueue a metric without blocking. Returns false (dropped) if the buffer is full.</summary>
    public bool TryPublish(RunMetricEntry entry) => _channel.Writer.TryWrite(entry);

    /// <summary>Signal that no more metrics will be published.</summary>
    public void Complete() => _channel.Writer.Complete();

    /// <summary>Drain the channel, calling <paramref name="writeBatch"/> with up to <paramref name="batchSize"/> items.</summary>
    public async Task DrainAsync(Func<IReadOnlyList<RunMetricEntry>, Task> writeBatch, int batchSize = 32, CancellationToken ct = default)
    {
        var buffer = new List<RunMetricEntry>(batchSize);
        await foreach (var item in _channel.Reader.ReadAllAsync(ct))
        {
            buffer.Add(item);
            if (buffer.Count >= batchSize)
            {
                await writeBatch(buffer);
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            await writeBatch(buffer);
        }
    }
}
