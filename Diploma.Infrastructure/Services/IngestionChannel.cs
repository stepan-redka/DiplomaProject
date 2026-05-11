using System.Threading.Channels;
using Diploma.Application.DTOs;

namespace Diploma.Infrastructure.Services;

/// <summary>
/// A singleton wrapper around a System.Threading.Channels.Channel for document ingestion tasks.
/// </summary>
public class IngestionChannel
{
    private readonly Channel<IngestionTask> _channel;

    public IngestionChannel()
    {
        // Unbounded for now, but in a heavy-load environment, we would use Bounded 
        // to implement backpressure and prevent OOM.
        _channel = Channel.CreateUnbounded<IngestionTask>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelWriter<IngestionTask> Writer => _channel.Writer;
    public ChannelReader<IngestionTask> Reader => _channel.Reader;
}
