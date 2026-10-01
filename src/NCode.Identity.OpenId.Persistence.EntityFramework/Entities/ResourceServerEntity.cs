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
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

/// <summary>
/// Represents an entity framework data contract for an <c>OAuth</c> resource server (an API, in Auth0 terms). A
/// resource server owns its scopes and is identified by its audience <see cref="Identifier"/>.
/// The complimentary DTO for this entity is <c>PersistedResourceServer</c>.
/// </summary>
[Index(nameof(TenantId), nameof(NormalizedResourceServerId), IsUnique = true)]
[Index(nameof(TenantId), nameof(NormalizedIdentifier), IsUnique = true)]
[Index(nameof(NormalizedTenantId), IsUnique = false)]
public sealed class ResourceServerEntity : ISupportTenantEntity, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <inheritdoc />
    [ForeignKey(nameof(Tenant))]
    public required long TenantId { get; init; }

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedTenantId { get; init; }

    /// <summary>
    /// Gets the opaque, server-generated public identifier for this resource server.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string ResourceServerId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="ResourceServerId"/> in uppercase so that lookups can be sargable for DBMS engines
    /// that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedResourceServerId { get; init; }

    /// <summary>
    /// Gets the operator-supplied audience identifier for this resource server.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.ResourceServerIdentifier)]
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets the value of <see cref="Identifier"/> in uppercase so that lookups can be sargable for DBMS engines that
    /// don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.ResourceServerIdentifier)]
    public required string NormalizedIdentifier { get; init; }

    //

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token for the resource server's scopes.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ScopesConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the human-readable name for this resource server.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.DisplayName)]
    public required string Name { get; set; }

    /// <summary>
    /// Gets a value indicating whether this resource server is a reserved, system-owned resource server (such as the
    /// one that owns the standard OpenID Connect scopes) that cannot be deleted.
    /// </summary>
    public required bool IsSystem { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this resource server is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the resource server settings.
    /// </summary>
    public required JsonElement SettingsJson { get; set; }

    // navigational properties

    /// <inheritdoc />
    public required TenantEntity Tenant { get; init; }

    /// <summary>
    /// Gets the collection of scopes owned by this resource server.
    /// </summary>
    public required IEnumerable<ScopeEntity> Scopes { get; init; }
}
