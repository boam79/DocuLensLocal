namespace DocuLensLocal.Core;

public sealed class DebouncedAction : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly Func<CancellationToken, Task> _action;
    private readonly object _gate = new();
    private CancellationTokenSource? _pending;
    private bool _disposed;

    public DebouncedAction(TimeSpan delay, Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        _delay = delay;
        _action = action;
    }

    public void Ping()
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _pending?.Cancel();
            _pending = new CancellationTokenSource();
            cts = _pending;
        }

        _ = WaitThenRun(cts);
    }

    private async Task WaitThenRun(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_delay, cts.Token).ConfigureAwait(false);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            await _action(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pending?.Cancel();
            _pending?.Dispose();
            _pending = null;
        }
    }
}
