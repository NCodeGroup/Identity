// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence;

/// <summary>
/// Indicates that an instance supports the <see cref="ServerId"/> property.
/// </summary>
[PublicAPI]
public interface ISupportServerId
{
    /// <summary>
    /// Gets the identifier of the OpenId Server that this instance belongs to.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    string ServerId { get; }
}
