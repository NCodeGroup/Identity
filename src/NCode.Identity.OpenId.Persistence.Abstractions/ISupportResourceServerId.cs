// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence;

/// <summary>
/// Indicates that an instance supports the <see cref="ResourceServerId"/> property.
/// </summary>
[PublicAPI]
public interface ISupportResourceServerId
{
    /// <summary>
    /// Gets the identifier of the OpenId Resource Server that this instance belongs to.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    string ResourceServerId { get; }
}
