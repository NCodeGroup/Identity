// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence;

/// <summary>
/// Indicates that an instance supports the <see cref="ClientId"/> property.
/// </summary>
[PublicAPI]
public interface ISupportClientId
{
    /// <summary>
    /// Gets the identifier of the OpenId Client that this instance belongs to.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    string ClientId { get; }
}
