// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options of the <see cref="ISecretVerificationGate"/> that every secret verification with a work factor
/// takes a turn of, whatever its scheme. They are validated as the host starts.
/// </summary>
// See ADR-1391.
[PublicAPI]
public sealed class SecretVerificationGateOptions
{
    /// <summary>
    /// Gets or sets the most verifications the process runs at once. A verification beyond it waits for a turn, without
    /// holding a thread, so presented secrets that cost a work factor never occupy more processors than this however
    /// many arrive. The default is half the processors; it must be at least <c>1</c>.
    /// </summary>
    public int MaxConcurrency { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>
    /// Gets or sets the most verifications that wait for a turn while <see cref="MaxConcurrency"/> verifications run. A
    /// verification that arrives when this many already wait is not compared at all and reports
    /// <see cref="SecretVerificationResult.Unavailable"/>, so a flood of presented secrets fails fast instead of
    /// queuing without bound. The default is <c>16</c>; it must be at least <c>0</c>, and <c>0</c> refuses every
    /// verification that finds no turn free.
    /// </summary>
    public int MaxQueueLength { get; set; } = 16;

    /// <summary>
    /// Gets or sets how long a verification waits for a turn before it gives up and reports
    /// <see cref="SecretVerificationResult.Unavailable"/> without comparing. The default is two seconds; it must be
    /// from one millisecond to <see cref="int.MaxValue"/> milliseconds.
    /// </summary>
    public TimeSpan QueueTimeout { get; set; } = TimeSpan.FromSeconds(2);
}
