// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the REST resource for the usage of a team's tenants on a range of UTC calendar days, read from the
/// durable daily ledger the platform bills a plan by.
/// </summary>
// See ADR-1411
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TeamUsageResource
{
    /// <summary>
    /// Gets the identifier of the team.
    /// </summary>
    public required string TeamId { get; init; }

    /// <summary>
    /// Gets the first UTC calendar day of the range, inclusive.
    /// </summary>
    public required DateOnly From { get; init; }

    /// <summary>
    /// Gets the last UTC calendar day of the range, inclusive.
    /// </summary>
    public required DateOnly To { get; init; }

    /// <summary>
    /// Gets the usage of each meter by each tenant on each day of the range that has any. A day, tenant or meter
    /// without an entry had no usage.
    /// </summary>
    public required IReadOnlyList<TeamUsageEntry> Entries { get; init; }
}
