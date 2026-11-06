// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures the pass that ends the stored sessions of removed or re-registered frontends.
/// </summary>
[PublicAPI]
public sealed class BffSessionRetirementOptions
{
    /// <summary>
    /// Gets or sets how long one replica holds the lease of a frontend whose sessions it ends. The default is 5
    /// minutes. The configuration key is <c>Bff:SessionRetirement:LeaseDuration</c>.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long the BFF waits before it repeats a pass that failed or left a frontend to another holder of
    /// its lease. The default is 1 minute. The configuration key is <c>Bff:SessionRetirement:RetryDelay</c>.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);
}
