using System;
using System.Collections.Concurrent;
using System.Threading;

namespace OpennessCopy.Services;

/// <summary>
/// Simple blocking queue for marshaling work items onto the STA workflow thread.
/// Shared by both PLC and hardware workflows to avoid per-workflow synchronization plumbing.
/// </summary>
internal sealed class StaTaskQueue : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();

    /// <summary>
    /// Attempts to enqueue a work item. Returns false when the queue is shutting down.
    /// </summary>
    public bool TryAdd(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (_queue.IsAddingCompleted)
        {
            return false;
        }

        try
        {
            _queue.Add(action);
            return true;
        }
        catch (InvalidOperationException)
        {
            // Adding was already completed by another thread.
            return false;
        }
    }

    /// <summary>
    /// Attempts to take the next work item, honoring cancellation or queue completion.
    /// </summary>
    public bool TryTake(out Action workItem, CancellationToken cancellationToken)
    {
        workItem = null;

        try
        {
            return _queue.TryTake(out workItem, Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // Completed and empty.
            return false;
        }
    }

    /// <summary>
    /// Stops accepting new work and drains remaining items.
    /// </summary>
    public void Complete()
    {
        if (!_queue.IsAddingCompleted)
        {
            _queue.CompleteAdding();
        }
    }

    public bool IsAddingCompleted => _queue.IsAddingCompleted;

    public void Dispose()
    {
        _queue.Dispose();
    }
}
