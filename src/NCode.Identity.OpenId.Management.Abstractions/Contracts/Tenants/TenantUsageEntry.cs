// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents a tenant's usage of one meter on one UTC calendar day, an entry of
/// <see cref="TenantUsageResource.Entries"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantUsageEntry
{
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
