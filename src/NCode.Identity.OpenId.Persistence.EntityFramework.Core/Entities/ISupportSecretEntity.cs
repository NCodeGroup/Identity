// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Indicates that an entity supports the <see cref="Secret"/> navigation property
/// and the <see cref="SecretId"/> foreign key.
/// </summary>
[PublicAPI]
public interface ISupportSecretEntity
{
    /// <summary>
    /// Gets the foreign key for the associated secret.
    /// </summary>
    long SecretId { get; }

    /// <summary>
    /// Gets the navigation property for the associated secret.
    /// </summary>
    SecretEntity Secret { get; }
}
