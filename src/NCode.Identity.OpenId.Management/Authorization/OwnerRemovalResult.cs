// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Describes the outcome of removing an owner from a resource.
/// </summary>
[PublicAPI]
public enum OwnerRemovalResult
{
    /// <summary>
    /// The owner assignment was removed.
    /// </summary>
    Removed = 0,

    /// <summary>
    /// The principal was not an owner of the resource; nothing was removed.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// The removal was refused because it would leave the resource with no owner.
    /// </summary>
    LastOwnerForbidden = 2,
}
