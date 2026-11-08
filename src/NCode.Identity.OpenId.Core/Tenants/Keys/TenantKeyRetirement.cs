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
    /// Gets the instant the key activates at: its own, or the retirement for a key published ahead of an activation
    /// that never comes, so its active window collapses to nothing and the instants stay in order.
    /// </summary>
    public required DateTimeOffset ActivatesWhen { get; init; }

    /// <summary>
    /// Gets the instant the key stops signing or encrypting.
    /// </summary>
    public required DateTimeOffset RetiresWhen { get; init; }

    /// <summary>
    /// Gets the instant the key expires: the end of the retirement grace, which covers every token it protected.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets the reason for the retirement, one of the <see cref="TenantKeyChangeReasons"/>.
    /// </summary>
    public required string Reason { get; init; }
}
