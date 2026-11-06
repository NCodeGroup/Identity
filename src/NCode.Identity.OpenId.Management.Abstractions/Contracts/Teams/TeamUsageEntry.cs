// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the usage of one meter by one of a team's tenants on one UTC calendar day, an entry of
/// <see cref="TeamUsageResource.Entries"/>.
/// </summary>
// See ADR-1411
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TeamUsageEntry
{
    /// <summary>
    /// Gets the identifier of the team the tenant belonged to.
    /// </summary>
    public required string TeamId { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant that used the meter.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the UTC calendar day.
    /// </summary>
    public required DateOnly Day { get; init; }

    /// <summary>
    /// Gets the name of the meter, such as <c>requests</c> or <c>token_requests</c>. Meters are an open vocabulary.
    /// </summary>
    public required string Meter { get; init; }

    /// <summary>
    /// Gets the count of the meter on the day.
    /// </summary>
    public required long Count { get; init; }
}
