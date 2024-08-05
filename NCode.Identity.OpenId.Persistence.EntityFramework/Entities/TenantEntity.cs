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
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

/// <summary>
/// Represents an entity framework data contract for an <c>OAuth</c> or <c>OpenID Connect</c> tenant.
/// The complimentary DTO for this entity is <see cref="PersistedTenant"/>.
/// </summary>
[Index(nameof(NormalizedTenantId), IsUnique = true)]
[Index(nameof(NormalizedDomainName), IsUnique = true)]
public class TenantEntity : ISupportId, ISupportConcurrencyToken
{
    /// <inheritdoc />
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <summary>
    /// Gets or sets the natural identifier for this entity.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.TenantId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the value of <see cref="TenantId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.TenantId)]
    public required string NormalizedTenantId { get; init; }

    //

    /// <summary>
    /// Gets or sets the domain name for this entity.
    /// This value is optional and can be used to find tenants by domain name.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.TenantDomainName)]
    public required string? DomainName { get; init; }

    /// <summary>
    /// Gets or sets the value of <see cref="DomainName"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.TenantDomainName)]
    public required string? NormalizedDomainName { get; init; }

    //

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

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
    public required JsonElement Settings { get; set; }

    //

    // ReSharper disable once EntityFramework.ModelValidation.CircularDependency
    // We use DTOs to avoid circular dependencies.
    /// <summary>
    /// Gets or sets the collection of secrets only known to this tenant.
    /// </summary>
    public required IEnumerable<TenantSecretEntity> Secrets { get; init; }
}
