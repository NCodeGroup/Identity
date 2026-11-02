// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for a relationship between a <see cref="ServerEntity"/> and a <see cref="SecretEntity"/>.
/// </summary>
[Index(nameof(ServerId), nameof(SecretId), IsUnique = true)]
public sealed class ServerSecretEntity : ISupportSecretEntity
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <summary>
    /// Gets the foreign key for the associated server.
    /// </summary>
    [ForeignKey(nameof(Server))]
    public required long ServerId { get; init; }

    /// <inheritdoc />
    [ForeignKey(nameof(Secret))]
    public required long SecretId { get; init; }

    // navigation properties

    /// <summary>
    /// Gets the navigation property for the associated server.
    /// </summary>
    public required ServerEntity Server { get; init; }

    // ReSharper disable once EntityFramework.ModelValidation.CircularDependency
    // We use DTOs to avoid circular dependencies.
    /// <inheritdoc />
    public required SecretEntity Secret { get; init; }
}
