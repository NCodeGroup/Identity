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
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for a federated principal: a tenant-scoped representation of a human
/// actor that owns many external connection identities (<see cref="FederatedIdentityEntity"/>). The complimentary DTO
/// for this entity is <c>PersistedFederatedPrincipal</c>.
/// </summary>
[Index(nameof(TenantId), nameof(NormalizedPrincipalId), IsUnique = true)]
public sealed class FederatedPrincipalEntity : ISupportTenantEntity, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <summary>
    /// Gets the server-generated opaque public identifier of the principal.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.PrincipalId)]
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="PrincipalId"/> in lowercase so that lookups can be sargable for DBMS engines that
    /// don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.PrincipalId)]
    public required string NormalizedPrincipalId { get; init; }

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

    //

    /// <summary>
    /// Gets or sets the principal's owner-updatable profile metadata as a free-form JSON object, defaulting to an
    /// empty object <c>{}</c>; converted to a string column by the shared JsonElement value converter (ADR-0055).
    /// </summary>
    public required JsonElement ProfileMetadataJson { get; set; }

    /// <summary>
    /// Gets or sets the principal's server-controlled system metadata as a free-form JSON object, defaulting to an
    /// empty object <c>{}</c>; never end-user writable, as it may carry authorization-relevant data (ADR-0055).
    /// </summary>
    public required JsonElement SystemMetadataJson { get; set; }

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    // navigation properties

    /// <summary>
    /// Gets the navigation property for the associated tenant.
    /// </summary>
    public required TenantEntity Tenant { get; init; }
}
