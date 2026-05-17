using SystemMonitor.Shared.Models;
using Microsoft.Extensions.Options;

using SystemMonitorAgent.Configuration;

namespace SystemMonitorAgent.BackgroundTasks;

public sealed class PayloadQueue
{
    private readonly LinkedList<AgentPayload> _queue = new();
    private readonly object _lock = new();
    private readonly int _maxItems;

    public PayloadQueue(IOptions<AgentOptions> options)
    {
        _maxItems = options.Value.RetryQueueMaxItems;
    }

    public ValueTask EnqueueAsync(AgentPayload payload, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_queue.Count >= _maxItems)
            {
                _queue.RemoveFirst();
            }
            _queue.AddLast(payload);
        }
        return ValueTask.CompletedTask;
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    public bool TryPeek(out AgentPayload? payload)
    {
        lock (_lock)
        {
            if (_queue.First != null)
            {
                payload = _queue.First.Value;
                return true;
            }
            payload = null;
            return false;
        }
    }

    public bool TryDequeue(out AgentPayload? payload)
    {
        lock (_lock)
        {
            if (_queue.First != null)
            {
                payload = _queue.First.Value;
                _queue.RemoveFirst();
                return true;
            }
            payload = null;
            return false;
        }
    }
}
