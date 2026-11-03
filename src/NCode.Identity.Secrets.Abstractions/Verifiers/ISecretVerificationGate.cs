// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Verifiers;

/// <summary>
/// Provides the turns that costly secret verifications take, shared by every <see cref="SecretVerifier"/> in the
/// process whose <see cref="SecretVerifier.HasWorkFactor"/> is set, whatever its scheme: at most a fixed number run at
/// once, at most a fixed number wait for a turn, and a waiter gives up after a fixed time. A verification that finds
/// the queue full or waits too long is not run, and its verifier answers
/// <see cref="SecretVerificationResult.Unavailable"/>, so a flood of presented secrets neither occupies every
/// processor nor queues without bound.
/// </summary>
// See ADR-1391.
[PublicAPI]
public interface ISecretVerificationGate
{
    /// <summary>
    /// Takes a turn, waiting for one when none is free. A turn taken is given back with <see cref="Release"/>.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the wait.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation, containing
    /// <see langword="true"/> when a turn was taken, or <see langword="false"/> when the queue was full or no turn came
    /// in time.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gives back a turn that <see cref="TryEnterAsync"/> took.
    /// </summary>
    void Release();
}
