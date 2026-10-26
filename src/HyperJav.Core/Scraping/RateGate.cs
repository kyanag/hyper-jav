namespace HyperJav.Core.Scraping;

/// <summary>按站限速门：保证相邻两次放行的起始时间至少间隔 minIntervalMs。</summary>
public sealed class RateGate(int minIntervalMs)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;

    public async Task WaitForTurnAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var wait = _nextAllowed - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }
            _nextAllowed = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(minIntervalMs);
        }
        finally
        {
            _gate.Release();
        }
    }
}
