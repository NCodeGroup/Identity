// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Describes a key the rollover retires: the key as the tenant holds it, the instant it stops signing or encrypting,
/// and the instant it expires, which the retirement grace bounds.
/// </summary>
// See ADR-1404.
internal readonly record struct TenantKeyRetirement
{
    /// <summary>
    /// Gets the key as the tenant holds it.
    /// </summary>
    public required PersistedSecret Secret { get; init; }

    /// <summary>
    /// Gets the instant the key stops signing or encrypting.
    /// </summary>
    public required DateTimeOffset RetiresWhen { get; init; }

    /// <summary>
    /// Gets the instant the key expires: the earlier of its own expiry and the end of the retirement grace.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
