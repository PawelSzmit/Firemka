namespace Firemka.Web.Backups;

public sealed class BackupExportGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IDisposable?> TryEnterAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken)) return null;
        return new Releaser(_gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) gate.Release();
        }
    }
}
