// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for an issuer a tenant gave up, by changing its issuer or by being
/// deleted. Relying parties that trusted the issuer would trust any tenant that took it, so only the tenant that held it
/// can take it back (ADR-1189).
/// </summary>
[Index(nameof(NormalizedIssuer), IsUnique = true)]
public sealed class RetiredIssuerEntity
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public long Id { get; init; }

    /// <summary>
    /// Gets the retired issuer.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.Issuer)]
    public required string Issuer { get; init; }

    /// <summary>
    /// Gets the value of <see cref="Issuer"/> normalized so that lookups can be sargable for DBMS engines that don't
    /// support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.Issuer)]
    public required string NormalizedIssuer { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant that held the issuer.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }
}
