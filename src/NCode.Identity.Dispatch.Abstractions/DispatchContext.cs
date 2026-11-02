// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Dispatch;

/// <summary>
/// Carries the ambient state of a single execution attempt for a dispatched job, passed to the
/// <see cref="IDispatchJobHandler{TJob}"/> so a handler can make a delivery idempotent and reason about retries.
/// </summary>
[PublicAPI]
public readonly record struct DispatchContext
{
    /// <summary>
    /// Gets the stable identifier of the dispatched job that is constant across every retry attempt. A handler that
    /// performs an at-least-once side effect should key its de-duplication on this value, since a dispatcher adapter
    /// may redeliver the same job.
    /// </summary>
    public required Guid DispatchId { get; init; }

    /// <summary>
    /// Gets the one-based number of the current execution attempt.
    /// </summary>
    public required int Attempt { get; init; }

    /// <summary>
    /// Gets the maximum number of attempts the dispatcher will make before abandoning the job.
    /// </summary>
    public required int MaxAttempts { get; init; }
}
