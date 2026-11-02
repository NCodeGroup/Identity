// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted federated identity: one external connection identity owned by a federated
/// principal. The upstream <see cref="Issuer"/> + <see cref="Subject"/> pair is the lookup-only natural key; the owning
/// principal is referenced by <see cref="PrincipalId"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedFederatedIdentity : ISupportTenantId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the identifier of the tenant that owns this identity.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the server-generated opaque public identifier of this federated identity.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string FederatedIdentityId { get; init; }

    /// <summary>
    /// Gets the identifier of the federated principal that owns this identity.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.PrincipalId)]
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the upstream issuer that authenticated this connection identity.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.Issuer)]
    public required string Issuer { get; init; }

    /// <summary>
    /// Gets the upstream subject value (the OpenID <c>sub</c>) as issued by the <see cref="Issuer"/>.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.SubjectId)]
    public required string Subject { get; init; }

    /// <summary>
    /// Gets the optional join key (such as a verified email address) asserted by this connection identity,
    /// used by the linking policy to attach a new identity to an existing principal. This value is <c>null</c> when
    /// the connection asserts no usable join key.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.JoinKey)]
    public required string? JoinKey { get; init; }

    /// <inheritdoc />
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; set; }
}
