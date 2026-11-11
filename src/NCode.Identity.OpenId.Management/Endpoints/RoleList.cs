// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Describes one role list of a resource: the role whose holders the list names, the node of the resource hierarchy
/// the role is assigned at, the operation that authorizes a grant or revoke there, and where the list is served.
/// </summary>
// See ADR-1469.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RoleList
{
    /// <summary>
    /// Gets the resource node that the role is assigned at.
    /// </summary>
    public required ResourceNode Node { get; init; }

    /// <summary>
    /// Gets the name of the role whose holders the list names.
    /// </summary>
    public required string RoleName { get; init; }

    /// <summary>
    /// Gets the operation that authorizes a grant or revoke of the role at the node.
    /// </summary>
    public required IAuthorizationRequirement Operation { get; init; }

    /// <summary>
    /// Gets the path of the role list, which the <c>Location</c> of a grant is beneath.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the delegate that determines whether the resource of the node exists.
    /// </summary>
    public required Func<
        IStoreManager,
        CancellationToken,
        ValueTask<bool>
    > ResourceExistsAsync { get; init; }
}
