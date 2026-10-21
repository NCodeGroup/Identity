#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
    /// origin of every absolute URL the server publishes for it. Optional; when <c>null</c> the origin is derived from
    /// the incoming request, which is a development-only convenience.
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

    // navigation properties

    // ReSharper disable once EntityFramework.ModelValidation.CircularDependency
    // We use DTOs to avoid circular dependencies.
    /// <summary>
    /// Gets the collection of secrets only known to this tenant.
    /// </summary>
    public required IEnumerable<TenantSecretEntity> Secrets { get; init; }
}
