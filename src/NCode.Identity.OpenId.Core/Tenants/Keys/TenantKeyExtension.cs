// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Describes a key the rollover keeps published for longer: its retirement plus the retirement grace, as the tenant's
/// settings and token lifetimes stand, reaches past its expiry, so the expiry moves to the end of the grace and no
/// token the key protected outlives it. An expiry only ever moves later.
/// </summary>
// See ADR-1404.
internal readonly record struct TenantKeyExtension
{
    /// <summary>
    /// Gets the key as the tenant holds it.
    /// </summary>
    public required PersistedSecret Secret { get; init; }

    /// <summary>
    /// Gets the instant the key expires at instead.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
