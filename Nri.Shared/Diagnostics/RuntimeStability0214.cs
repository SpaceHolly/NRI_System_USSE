using System;
using System.Collections.Generic;
using System.Threading;

namespace Nri.Shared.Diagnostics;

public sealed class NonOverlappingOperationGate0214
{
    private int _active;
    private long _prevented;

    public bool TryEnter()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) == 0) return true;
        Interlocked.Increment(ref _prevented);
        return false;
    }

    public void Exit() => Interlocked.Exchange(ref _active, 0);
    public bool IsActive => Volatile.Read(ref _active) != 0;
    public long PreventedOverlapCount => Interlocked.Read(ref _prevented);
}

public sealed class RefreshCancellationCoordinator0214 : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource _current = new();
    private long _generation;
    private bool _disposed;

    public RefreshLease0214 Begin()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _current.Cancel();
            _current.Dispose();
            _current = new CancellationTokenSource();
            _generation++;
            return new RefreshLease0214(_generation, _current.Token);
        }
    }

    public bool IsCurrent(RefreshLease0214 lease)
    {
        lock (_gate) return !_disposed && lease.Generation == _generation && !lease.Token.IsCancellationRequested;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _current.Cancel();
            _current.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RefreshCancellationCoordinator0214));
    }
}

public readonly struct RefreshLease0214
{
    public RefreshLease0214(long generation, CancellationToken token)
    {
        Generation = generation;
        Token = token;
    }

    public long Generation { get; }
    public CancellationToken Token { get; }
}

public sealed class BoundedIdentityWindow0214
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly HashSet<string> _identities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _order = new();

    public BoundedIdentityWindow0214(int capacity = 2048)
    {
        if (capacity < 16 || capacity > 65536) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Capacity => _capacity;
    public int Count { get { lock (_gate) return _identities.Count; } }

    public bool TryAdd(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return false;
        lock (_gate)
        {
            if (!_identities.Add(identity)) return false;
            _order.Enqueue(identity);
            while (_order.Count > _capacity)
            {
                _identities.Remove(_order.Dequeue());
            }
            return true;
        }
    }
}
