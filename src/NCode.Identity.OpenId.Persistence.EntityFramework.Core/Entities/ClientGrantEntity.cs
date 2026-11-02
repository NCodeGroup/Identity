// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for a client grant (in Auth0 terms): a client's authorization to a
/// <see cref="ResourceServerEntity"/> with a set of granted scopes. The granted scope values are stored as a JSON
/// array; their validity is enforced at write time and intersected with the resource server's current scopes at
/// read time. The complimentary DTO for this entity is <c>PersistedClientGrant</c>.
/// </summary>
[Index(nameof(TenantId), nameof(ClientId), nameof(ResourceServerId), IsUnique = true)]
public sealed class ClientGrantEntity : ISupportTenantEntity, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <inheritdoc />
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
    /// Gets the foreign key for the client that is authorized.
    /// </summary>
    [ForeignKey(nameof(Client))]
    public required long ClientId { get; init; }

    /// <summary>
    /// Gets the foreign key for the resource server the client is authorized to.
    /// </summary>
    [ForeignKey(nameof(ResourceServer))]
    public required long ResourceServerId { get; init; }

    //

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON array of granted scope values.
    /// </summary>
    public required JsonElement ScopesJson { get; set; }

    // navigational properties

    /// <inheritdoc />
    public required TenantEntity Tenant { get; init; }

    /// <summary>
    /// Gets the navigation property for the authorized client.
    /// </summary>
    public required ClientEntity Client { get; init; }

    /// <summary>
    /// Gets the navigation property for the resource server the client is authorized to.
    /// </summary>
    public required ResourceServerEntity ResourceServer { get; init; }
}
