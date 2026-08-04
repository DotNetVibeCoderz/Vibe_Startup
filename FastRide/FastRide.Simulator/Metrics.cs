using System.Collections.Concurrent;

namespace FastRide.Simulator;

/// <summary>
/// Counters and latency samples for the run.
///
/// The old simulator only tracked "API ok / API fail", which cannot tell you whether the
/// platform is healthy or merely slow. Latency percentiles are the point of a load run.
/// </summary>
public sealed class Metrics
{
    private readonly ConcurrentQueue<double> _latencies = new();
    private int _requests, _failures;
    private int _ordersCreated, _ordersAccepted, _ordersCompleted, _ordersCancelled, _reviews;
    private int _paymentsSettled, _paymentsFailed;

    public int Requests => Volatile.Read(ref _requests);
    public int Failures => Volatile.Read(ref _failures);
    public int OrdersCreated => Volatile.Read(ref _ordersCreated);
    public int OrdersAccepted => Volatile.Read(ref _ordersAccepted);
    public int OrdersCompleted => Volatile.Read(ref _ordersCompleted);
    public int OrdersCancelled => Volatile.Read(ref _ordersCancelled);
    public int Reviews => Volatile.Read(ref _reviews);
    public int PaymentsSettled => Volatile.Read(ref _paymentsSettled);
    public int PaymentsFailed => Volatile.Read(ref _paymentsFailed);

    public void Record(double milliseconds, bool success)
    {
        Interlocked.Increment(ref _requests);
        if (!success) Interlocked.Increment(ref _failures);

        _latencies.Enqueue(milliseconds);

        // Keep the window bounded; a long run must not grow without limit.
        while (_latencies.Count > 5000 && _latencies.TryDequeue(out _))
        {
        }
    }

    public void OrderCreated() => Interlocked.Increment(ref _ordersCreated);
    public void OrderAccepted() => Interlocked.Increment(ref _ordersAccepted);
    public void OrderCompleted() => Interlocked.Increment(ref _ordersCompleted);
    public void OrderCancelled() => Interlocked.Increment(ref _ordersCancelled);
    public void ReviewSubmitted() => Interlocked.Increment(ref _reviews);
    public void PaymentSettled() => Interlocked.Increment(ref _paymentsSettled);
    public void PaymentFailed() => Interlocked.Increment(ref _paymentsFailed);

    public (double P50, double P95, double Max) Latency()
    {
        var samples = _latencies.ToArray();
        if (samples.Length == 0) return (0, 0, 0);

        Array.Sort(samples);
        return (Percentile(samples, 0.50), Percentile(samples, 0.95), samples[^1]);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}
