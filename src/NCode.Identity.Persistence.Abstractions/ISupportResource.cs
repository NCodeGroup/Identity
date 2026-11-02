// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace NCode.Identity.Persistence;

/// <summary>
/// Indicates that an instance supports the <see cref="ResourceType"/> and <see cref="ResourceId"/> properties.
/// </summary>
[PublicAPI]
public interface ISupportResource
{
    /// <summary>
    /// Gets a value that indicates the type (i.e. discriminator) of the resource.
    /// </summary>
    [MaxLength(MaxLengths.ResourceType)]
    string ResourceType { get; }

    /// <summary>
    /// Gets the identifier of the resource.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    string ResourceId { get; }
}
