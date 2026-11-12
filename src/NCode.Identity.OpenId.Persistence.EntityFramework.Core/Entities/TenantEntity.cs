// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for an <c>OAuth</c> or <c>OpenID Connect</c> tenant.
/// The complimentary DTO for this entity is <see cref="PersistedTenant"/>.
/// </summary>
[Index(nameof(NormalizedTenantId), IsUnique = true)]
public sealed class TenantEntity : ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public long Id { get; init; }

    /// <summary>
    /// Gets the natural identifier for this entity.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="TenantId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedTenantId { get; init; }

    //

    /// <summary>
    /// Gets or sets the issuer identifier for this tenant — its request-independent identity, and therefore also the
    /// origin of every absolute URL the server publishes for it. <c>null</c> only for a tenant no strategy serves; a served
    /// tenant always has one, and nothing derives the origin from the incoming request (ADR-1165).
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.Issuer)]
    public required string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the value of <see cref="Issuer"/> in uppercase so that lookups can be sargable for DBMS engines
    /// that don't support case-insensitive indices. Uniquely indexed: two tenants must never share an issuer.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.Issuer)]
    public required string? NormalizedIssuer { get; set; }

    /// <summary>
    /// Gets or sets the host component of <see cref="Issuer"/> in uppercase, which is the routing key for the
    /// host-based tenant strategy.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="Issuer"/> and never set independently, so it cannot drift from it. Its index is
    /// deliberately <b>not</b> unique: path-based tenants legitimately share a host
    /// (<c>https://op.example/tenant-a</c>, <c>https://op.example/tenant-b</c>). Host uniqueness is a requirement only
    /// of the host-based strategy, so it is enforced by validation rather than by the schema.
    /// </remarks>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.TenantIssuerHost)]
    public required string? NormalizedIssuerHost { get; set; }

    /// <summary>
    /// Gets or sets the mutual-TLS endpoint-alias authority of the tenant (RFC 8705 §5).
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.Issuer)]
    public required string? MtlsEndpointAliasAuthority { get; set; }

    /// <summary>
    /// Gets or sets the normalized host component of <see cref="MtlsEndpointAliasAuthority"/>, the second routing key of
    /// the host-based tenant strategy. Derived from the authority on every write and never written independently.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.TenantIssuerHost)]
    public required string? NormalizedMtlsEndpointAliasHost { get; set; }

    //

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token for tenant's settings.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string SettingsConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token for the tenant's secrets.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string SecretsConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tenant is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the display name for the tenant.
    /// </summary>
    [Unicode]
    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
    // We never know how long the value will be.
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the tenant settings.
    /// </summary>
    public required JsonElement SettingsJson { get; set; }

    /// <summary>
    /// Gets the surrogate identifier of the team this tenant belongs to. A tenant never moves between teams.
    /// </summary>
    [ForeignKey(nameof(Team))]
    public required long TeamId { get; init; }

    // navigation properties

    /// <summary>
    /// Gets the team this tenant belongs to.
    /// </summary>
    public required TeamEntity Team { get; init; }

    // ReSharper disable once EntityFramework.ModelValidation.CircularDependency
    // We use DTOs to avoid circular dependencies.
    /// <summary>
    /// Gets the collection of secrets only known to this tenant.
    /// </summary>
    public required IEnumerable<TenantSecretEntity> Secrets { get; init; }
}
