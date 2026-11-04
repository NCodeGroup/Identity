// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Describes a published key the rollover activates ahead of the instant it was to activate at, because no key of its
/// algorithm signs or encrypts.
/// </summary>
// See ADR-1404.
internal readonly record struct TenantKeyActivation
{
    /// <summary>
    /// Gets the key as the tenant holds it.
    /// </summary>
    public required PersistedSecret Secret { get; init; }

    /// <summary>
    /// Gets the instant the key activates at instead.
    /// </summary>
    public required DateTimeOffset ActivatesWhen { get; init; }
}
