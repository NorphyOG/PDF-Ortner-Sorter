using System.Collections.Concurrent;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentBag<Task> _runningTasks = new();

    public BackgroundJobQueue(int maxConcurrency = 4)
    {
        if (maxConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        }

        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public Task QueueAsync(Func<CancellationToken, Task> workItem, CancellationToken cancellationToken)
    {
        if (workItem is null)
        {
            throw new ArgumentNullException(nameof(workItem));
        }

        var task = Task.Run(async () =>
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await workItem(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }, cancellationToken);

        _runningTasks.Add(task);
        return task;
    }

    public async ValueTask DisposeAsync()
    {
        while (_runningTasks.TryTake(out var task))
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Individual background tasks surface their own errors to the UI; swallow here.
            }
        }

        _semaphore.Dispose();
    }
}
