// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Manages resource ownership — the <see cref="BuiltInRoles.Owner"/> role assignments at a resource node.
/// Applications may use this to grant ownership of their own resource types by passing an application-defined
/// <c>resourceType</c>.
/// </summary>
[PublicAPI]
public interface IResourceOwnershipService
{
    /// <summary>
    /// Assigns the calling principal the <see cref="BuiltInRoles.Owner"/> role at the newly-created resource node,
    /// enlisted in the supplied unit of work so it commits atomically with the resource. When the caller
    /// resolves to no principal, the resource starts with no owner and the administrators above it manage it.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request, whose tenant settings
    /// configure how the creating principal is resolved.</param>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> that created the resource.</param>
    /// <param name="storeManager">The unit of work the resource is being created in.</param>
    /// <param name="tenantId">The identifier of the tenant that owns the resource.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the assignment
    /// that was added, or <see langword="null"/> when the caller resolves to no principal.</returns>
    ValueTask<PersistedRoleAssignment?> AssignCreatorAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        IStoreManager storeManager,
        string tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the <see cref="BuiltInRoles.Owner"/> assignments at the specified resource node.
    /// </summary>
    /// <param name="storeManager">The unit of work to read through.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the owner
    /// assignments at the node.</returns>
    ValueTask<IReadOnlyList<PersistedRoleAssignment>> GetOwnersAsync(
        IStoreManager storeManager,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Grants the specified principal the <see cref="BuiltInRoles.Owner"/> role at the resource node, enlisted in the
    /// supplied unit of work. The operation is idempotent: when the principal is already an owner, the existing
    /// assignment is returned unchanged.
    /// </summary>
    /// <param name="storeManager">The unit of work to enlist in.</param>
    /// <param name="tenantId">The identifier of the tenant that owns the resource.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <param name="principalId">The identifier of the principal to grant ownership to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the owner
    /// assignment and whether the grant added it.</returns>
    ValueTask<RoleAssignmentGrant> AddOwnerAsync(
        IStoreManager storeManager,
        string tenantId,
        string resourceType,
        string resourceId,
        string principalId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes the specified principal's <see cref="BuiltInRoles.Owner"/> assignment from the resource node, enlisted
    /// in the supplied unit of work. The removal is refused when it would leave the resource with no owner.
    /// </summary>
    /// <param name="storeManager">The unit of work to enlist in.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <param name="principalId">The identifier of the principal to remove ownership from.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the outcome of the
    /// removal and the assignments it deleted.</returns>
    ValueTask<OwnerRemoval> RemoveOwnerAsync(
        IStoreManager storeManager,
        string resourceType,
        string resourceId,
        string principalId,
        CancellationToken cancellationToken
    );

    // See ADR-1411
    /// <summary>
    /// Gets the assignments of the specified role at the resource node.
    /// </summary>
    /// <param name="storeManager">The unit of work to read through.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the assignments
    /// of the role at the node.</returns>
    ValueTask<IReadOnlyList<PersistedRoleAssignment>> GetAssignmentsAsync(
        IStoreManager storeManager,
        string resourceType,
        string resourceId,
        string roleName,
        CancellationToken cancellationToken
    );

    // See ADR-1411
    /// <summary>
    /// Grants the specified principal a role at the resource node, enlisted in the supplied unit of work. The
    /// operation is idempotent: when the principal already holds the role at the node, the existing assignment is
    /// returned unchanged.
    /// </summary>
    /// <param name="storeManager">The unit of work to enlist in.</param>
    /// <param name="tenantId">The identifier of the tenant that owns the resource.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <param name="principalId">The identifier of the principal to grant the role to.</param>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// assignment and whether the grant added it.</returns>
    ValueTask<RoleAssignmentGrant> AddAssignmentAsync(
        IStoreManager storeManager,
        string tenantId,
        string resourceType,
        string resourceId,
        string principalId,
        string roleName,
        CancellationToken cancellationToken
    );

    // See ADR-1411
    /// <summary>
    /// Removes the specified principal's assignment of a role from the resource node, enlisted in the supplied unit
    /// of work.
    /// </summary>
    /// <param name="storeManager">The unit of work to enlist in.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <param name="principalId">The identifier of the principal to remove the role from.</param>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// assignments that were removed, which is empty when the principal did not hold the role.</returns>
    ValueTask<IReadOnlyList<PersistedRoleAssignment>> RemoveAssignmentAsync(
        IStoreManager storeManager,
        string resourceType,
        string resourceId,
        string principalId,
        string roleName,
        CancellationToken cancellationToken
    );
}
