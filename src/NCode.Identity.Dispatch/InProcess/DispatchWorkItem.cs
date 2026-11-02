// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Dispatch.InProcess;

/// <summary>
/// Represents a single job together with the scheduling state the in-process dispatcher tracks across retries.
/// </summary>
internal sealed record DispatchWorkItem
{
    /// <summary>
    /// Gets the stable identifier assigned when the job was first dispatched; constant across retries.
    /// </summary>
    public required Guid DispatchId { get; init; }

    /// <summary>
    /// Gets the job to execute.
    /// </summary>
    public required IDispatchJob Job { get; init; }

    /// <summary>
    /// Gets the scheduling and retry options for the job.
    /// </summary>
    public required DispatchOptions Options { get; init; }

    /// <summary>
    /// Gets the one-based number of the current execution attempt.
    /// </summary>
    public required int Attempt { get; init; }
}
