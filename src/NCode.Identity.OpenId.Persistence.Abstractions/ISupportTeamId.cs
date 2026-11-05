// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence;

/// <summary>
/// Indicates that an instance supports the <see cref="TeamId"/> property.
/// </summary>
// See ADR-1411
[PublicAPI]
public interface ISupportTeamId
{
    /// <summary>
    /// Gets the identifier of the OpenId Team that this instance belongs to.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    string TeamId { get; }
}
