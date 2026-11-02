// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents the REST resource for a tenant's usage on a range of UTC calendar days, read from the durable daily
/// ledger the platform bills a plan by.
/// </summary>
// See ADR-1199.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantUsageResource
{
    /// <summary>
    /// Gets the identifier of the tenant.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the first UTC calendar day of the range, inclusive.
    /// </summary>
    public required DateOnly From { get; init; }

    /// <summary>
    /// Gets the last UTC calendar day of the range, inclusive.
    /// </summary>
    public required DateOnly To { get; init; }

    /// <summary>
    /// Gets the usage of each meter on each day of the range that has any, ordered by day and then by meter. A day or
    /// meter without an entry had no usage.
    /// </summary>
    public required IReadOnlyList<TenantUsageEntry> Entries { get; init; }
}
