// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides the turns that costly secret verifications take: at most a fixed number run at once, at most a fixed number
/// wait for a turn, and a waiter gives up after a fixed time. A verification that finds the queue full or waits too long
/// is refused at once, so a flood of presented secrets neither occupies every processor nor queues without bound.
/// </summary>
// See ADR-1391.
internal sealed class SecretVerificationGate : IDisposable
{
    // The number of verifications waiting for a turn, an Interlocked target.
    private int _waiterCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretVerificationGate"/> class.
    /// </summary>
    /// <param name="maxConcurrency">The most verifications that run at once, at least <c>1</c>.</param>
    /// <param name="maxQueueLength">The most verifications that wait for a turn, at least <c>0</c>.</param>
    /// <param name="queueTimeout">How long a verification waits for a turn, valid for
    /// <see cref="IsValidQueueTimeout"/>.</param>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> that times the wait.</param>
    public SecretVerificationGate(
        int maxConcurrency,
        int maxQueueLength,
        TimeSpan queueTimeout,
        TimeProvider timeProvider
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxQueueLength, 0);
        if (!IsValidQueueTimeout(queueTimeout))
        {
            throw new ArgumentOutOfRangeException(
                nameof(queueTimeout),
                queueTimeout,
                $"The queue timeout must be from 1 millisecond to {int.MaxValue} milliseconds."
            );
        }

        Turns = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        MaxQueueLength = maxQueueLength;
        QueueTimeout = queueTimeout;
        TimeProvider = timeProvider;
    }

    private SemaphoreSlim Turns { get; }

    private int MaxQueueLength { get; }

    private TimeSpan QueueTimeout { get; }

    private TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the number of turns free now.
    /// </summary>
    internal int FreeTurnCount => Turns.CurrentCount;

    /// <summary>
    /// Gets the number of verifications waiting for a turn now.
    /// </summary>
    internal int WaiterCount => Volatile.Read(ref _waiterCount);

    /// <summary>
    /// Determines whether a queue timeout is one a timer accepts: from one millisecond to <see cref="int.MaxValue"/>
    /// milliseconds.
    /// </summary>
    /// <param name="queueTimeout">The queue timeout to evaluate.</param>
    /// <returns><see langword="true"/> when the queue timeout is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidQueueTimeout(TimeSpan queueTimeout) =>
        queueTimeout >= TimeSpan.FromMilliseconds(1)
        && queueTimeout <= TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>
    /// Takes a turn, waiting for one when none is free. A turn taken is given back with <see cref="Release"/>.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the wait.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation, containing
    /// <see langword="true"/> when a turn was taken, or <see langword="false"/> when the queue was full or no turn came
    /// within the queue timeout.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A free turn is taken without queuing; SemaphoreSlim.Wait(0) never blocks.
        if (Turns.Wait(0, CancellationToken.None))
        {
            return true;
        }

        if (Interlocked.Increment(ref _waiterCount) > MaxQueueLength)
        {
            Interlocked.Decrement(ref _waiterCount);
            return false;
        }

        try
        {
            using var timeout = new CancellationTokenSource(QueueTimeout, TimeProvider);
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token
            );
            try
            {
                await Turns.WaitAsync(wait.Token);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Only the queue timeout cancels the wait without the caller's token.
                return false;
            }
        }
        finally
        {
            Interlocked.Decrement(ref _waiterCount);
        }
    }

    /// <summary>
    /// Gives back a turn that <see cref="TryEnterAsync"/> took.
    /// </summary>
    public void Release() => Turns.Release();

    /// <inheritdoc />
    public void Dispose() => Turns.Dispose();
}
