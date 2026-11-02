// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Indicates that an instance supports the <see cref="SecretId"/> property.
/// </summary>
[PublicAPI]
public interface ISupportSecretId
{
    /// <summary>
    /// Gets the identifier of the OpenId Secret that this instance belongs to.
    /// Also known as <c>kid</c> or <c>Key ID</c>.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    string SecretId { get; }
}
