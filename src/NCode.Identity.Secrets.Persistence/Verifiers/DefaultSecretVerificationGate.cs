// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides the default implementation of <see cref="ISecretVerificationGate"/>, one per process, bounded by the
/// <see cref="SecretVerificationGateOptions"/>. The options are read when the first verification takes a turn, so
/// constructing the gate, and the schemes that share it, reads no options.
/// </summary>
// See ADR-1391.
internal sealed class DefaultSecretVerificationGate(
    IOptions<SecretVerificationGateOptions> optionsAccessor,
    TimeProvider timeProvider
) : ISecretVerificationGate, IDisposable
{
    // The options are validated when they are read, by SecretVerificationGateOptionsValidator, so the limits are in
    // range.
    private Lazy<SemaphoreSlim> Turns { get; } =
        new(
            () =>
                new SemaphoreSlim(
                    optionsAccessor.Value.MaxConcurrency,
                    optionsAccessor.Value.MaxConcurrency
                ),
            LazyThreadSafetyMode.ExecutionAndPublication
        );

    // The number of verifications waiting for a turn, an Interlocked target.
    private int _waiterCount;

    private IOptions<SecretVerificationGateOptions> OptionsAccessor { get; } = optionsAccessor;

    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <summary>
    /// Gets the number of free turns.
    /// </summary>
    internal int FreeTurnCount => Turns.Value.CurrentCount;

    /// <summary>
    /// Gets the number of verifications that wait for a turn.
    /// </summary>
    internal int WaiterCount => Volatile.Read(ref _waiterCount);

    /// <inheritdoc />
    public async ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var turns = Turns.Value;
        var options = OptionsAccessor.Value;

        // A free turn is taken without queuing; SemaphoreSlim.Wait(0) never blocks.
        if (turns.Wait(0, CancellationToken.None))
        {
            return true;
        }

        if (Interlocked.Increment(ref _waiterCount) > options.MaxQueueLength)
        {
            Interlocked.Decrement(ref _waiterCount);
            return false;
        }

        try
        {
            using var timeout = new CancellationTokenSource(options.QueueTimeout, TimeProvider);
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token
            );
            try
            {
                await turns.WaitAsync(wait.Token);
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

    /// <inheritdoc />
    public void Release() => Turns.Value.Release();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Turns.IsValueCreated)
        {
            Turns.Value.Dispose();
        }
    }
}
