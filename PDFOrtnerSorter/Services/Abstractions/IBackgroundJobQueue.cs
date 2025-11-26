namespace PDFOrtnerSorter.Services.Abstractions;

public interface IBackgroundJobQueue : IAsyncDisposable
{
    Task QueueAsync(Func<CancellationToken, Task> workItem, CancellationToken cancellationToken);
}
