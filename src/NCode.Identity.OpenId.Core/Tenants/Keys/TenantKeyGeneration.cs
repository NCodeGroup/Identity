// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Describes a key the rollover generates for a tenant: the provisioned key it is generated as, the instants of its
/// lifecycle, and why it is generated.
/// </summary>
// See ADR-1404.
internal readonly record struct TenantKeyGeneration
{
    /// <summary>
    /// Gets the provisioned key the generated key satisfies.
    /// </summary>
    public required TenantKeySpecification Specification { get; init; }

    /// <summary>
    /// Gets the instant the key starts to sign or encrypt.
    /// </summary>
    public required DateTimeOffset ActivatesWhen { get; init; }

    /// <summary>
    /// Gets the instant the key stops signing or encrypting.
    /// </summary>
    public required DateTimeOffset RetiresWhen { get; init; }

    /// <summary>
    /// Gets the instant the key expires and is no longer published.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets why the key is generated: one of the values in <see cref="TenantKeyChangeReasons"/>.
    /// </summary>
    public required string Reason { get; init; }
}
