// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for an entity an audited operation acted on, so a lookup finds every
/// record of a resource, whether it was the target or an owner of it.
/// </summary>
// See ADR-1303.
[Index(nameof(Type), nameof(ResourceId))]
public sealed class AuditRecordResourceEntity
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public long Id { get; init; }

    /// <summary>
    /// Gets the surrogate identifier of the audit record.
    /// </summary>
    public long AuditRecordId { get; init; }

    /// <summary>
    /// Gets the position of the resource in the record, the target first.
    /// </summary>
    public required int Ordinal { get; init; }

    /// <summary>
    /// Gets the type of the resource.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the identifier of the resource, or <see langword="null"/>.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public string? ResourceId { get; init; }

    /// <summary>
    /// Gets the audit record.
    /// </summary>
    public required AuditRecordEntity AuditRecord { get; init; }
}
