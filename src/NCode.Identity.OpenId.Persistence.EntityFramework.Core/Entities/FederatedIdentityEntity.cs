#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for a federated identity: one external connection identity owned by a
/// <see cref="FederatedPrincipalEntity"/>. The upstream <see cref="Issuer"/> + <see cref="Subject"/> pair is the
/// lookup-only natural key. The complimentary DTO for this entity is <c>PersistedFederatedIdentity</c>.
/// </summary>
[Index(nameof(TenantId), nameof(NormalizedFederatedIdentityId), IsUnique = true)]
[Index(nameof(TenantId), nameof(NormalizedIssuer), nameof(NormalizedSubject), IsUnique = true)]
[Index(nameof(TenantId), nameof(NormalizedJoinKey))]
public sealed class FederatedIdentityEntity : ISupportTenantEntity, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <summary>
    /// Gets the server-generated opaque public identifier of this federated identity.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string FederatedIdentityId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="FederatedIdentityId"/> in lowercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedFederatedIdentityId { get; init; }

    /// <summary>
    /// Gets the foreign key for the associated tenant.
    /// </summary>
    [ForeignKey(nameof(Tenant))]
    public required long TenantId { get; init; }

    /// <summary>
    /// Gets the owning tenant's normalized identifier, denormalized onto this entity so the tenant-scoping global
    /// query filter compares a single indexed column without a navigation join.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedTenantId { get; init; }

    /// <summary>
    /// Gets the foreign key to the <see cref="FederatedPrincipalEntity"/> that owns this identity.
    /// </summary>
    [ForeignKey(nameof(FederatedPrincipal))]
    public required long FederatedPrincipalId { get; init; }

    /// <summary>
    /// Gets the upstream issuer that authenticated this connection identity.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.Issuer)]
    public required string Issuer { get; init; }

    /// <summary>
    /// Gets the value of <see cref="Issuer"/> in lowercase so that lookups can be sargable for DBMS engines that don't
    /// support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.Issuer)]
    public required string NormalizedIssuer { get; init; }

    /// <summary>
    /// Gets the upstream subject value (the OpenID <c>sub</c>) as issued by the <see cref="Issuer"/>.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.SubjectId)]
    public required string Subject { get; init; }

    /// <summary>
    /// Gets the value of <see cref="Subject"/> in lowercase so that lookups can be sargable for DBMS engines that
    /// don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.SubjectId)]
    public required string NormalizedSubject { get; init; }

    /// <summary>
    /// Gets the optional join key (such as a verified email address) asserted by this connection identity, used by the
    /// linking policy to deterministically attach a new identity to an existing principal. This value is <c>null</c>
    /// when the connection asserts no usable join key.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.JoinKey)]
    public required string? JoinKey { get; init; }

    /// <summary>
    /// Gets the value of <see cref="JoinKey"/> in lowercase so that lookups can be sargable for DBMS engines that
    /// don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.JoinKey)]
    public required string? NormalizedJoinKey { get; init; }

    //

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    // navigational properties

    /// <summary>
    /// Gets the <see cref="FederatedPrincipalEntity"/> that owns this identity.
    /// </summary>
    public required FederatedPrincipalEntity FederatedPrincipal { get; init; }

    /// <summary>
    /// Gets the navigation property for the associated tenant.
    /// </summary>
    public required TenantEntity Tenant { get; init; }
}
