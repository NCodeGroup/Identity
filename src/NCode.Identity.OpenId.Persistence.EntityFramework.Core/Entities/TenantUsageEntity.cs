// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for a tenant's usage of one meter on one UTC calendar day (ADR-1199).
/// The tenant is referenced by identifier, not by a foreign key, so the ledger outlives a deleted tenant; replicas add
/// to an entry under its concurrency token.
/// </summary>
[Index(nameof(TenantId), nameof(Day), nameof(Meter), IsUnique = true)]
[Index(nameof(Day))]
public sealed class TenantUsageEntity : ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public long Id { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant whose usage this is.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the UTC calendar day the usage was counted on.
    /// </summary>
    public required DateOnly Day { get; init; }

    /// <summary>
    /// Gets the name of the meter.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.UsageMeter)]
    public required string Meter { get; init; }

    /// <summary>
    /// Gets or sets the count of the meter on the day.
    /// </summary>
    public long Count { get; set; }

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }
}
