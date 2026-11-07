// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Provides common functionality for management API endpoint handlers whose resources are ownable, including the
/// owner-management endpoints (list, grant, revoke) and the one-owner invariant.
/// </summary>
internal abstract class BaseOwnableApiEndpointHandler : BaseApiEndpointHandler
{
    /// <summary>
    /// Gets the factory used to open a unit of work for owner-management operations.
    /// </summary>
    protected abstract IStoreManagerFactory StoreManagerFactory { get; }

    /// <summary>
    /// Gets the service used to read and mutate resource ownership.
    /// </summary>
    protected abstract IResourceOwnershipService ResourceOwnershipService { get; }

    /// <summary>
    /// Gets the recorder used to write management audit events.
    /// </summary>
    protected abstract IManagementAuditRecorder ManagementAuditRecorder { get; }

    /// <summary>
    /// Records that a role assignment was granted or revoked at a resource node.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <param name="assignment">The role assignment that was granted or revoked.</param>
    /// <param name="changeType">One of the values in <see cref="ResourceChangeTypes"/>.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    // See ADR-1449.
    protected internal virtual ValueTask RecordRoleAssignmentChangedAsync(
        HttpContext httpContext,
        PersistedRoleAssignment assignment,
        string changeType,
        CancellationToken cancellationToken
    )
    {
        var ownerResource = ToOwnerResource(assignment);
        return ManagementAuditRecorder.RecordResourceChangeAsync(
            httpContext,
            new RoleAssignmentChangedAuditEvent
            {
                EventSource = AuditEventSources.Management,
                TenantId = assignment.TenantId,
                SubjectId = assignment.PrincipalId,
                ResourceId = assignment.AssignmentId,
                NodeType = assignment.ResourceType,
                NodeId = assignment.ResourceId,
                ChangeType = changeType,
                ResourceValues = ToResourceValues(ownerResource),
                Outcome = AuditOutcome.Success,
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Maps an owner role assignment to its <see cref="OwnerResource"/> representation.
    /// </summary>
    /// <param name="assignment">The owner role assignment to map.</param>
    /// <returns>The mapped <see cref="OwnerResource"/>.</returns>
    internal virtual OwnerResource ToOwnerResource(PersistedRoleAssignment assignment) =>
        new()
        {
            AssignmentId = assignment.AssignmentId,
            PrincipalId = assignment.PrincipalId,
            RoleName = assignment.RoleName,
        };

    /// <summary>
    /// Processes an HTTP <c>GET</c> for a resource's owners: authorizes the caller against the resource node, then
    /// returns the owner assignments. Responds <c>404</c> when the resource does not exist.
    /// </summary>
    protected internal virtual async ValueTask<IResult> ProcessListOwnersAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceType,
        string resourceId,
        Func<IStoreManager, CancellationToken, ValueTask<bool>> resourceExistsAsync,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, resourceType, resourceId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
            Operations.Read
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        if (!await resourceExistsAsync(storeManager, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var owners = await ResourceOwnershipService.GetOwnersAsync(
            storeManager,
            resourceType,
            resourceId,
            cancellationToken
        );
        return TypedResults.Json(
            new CollectionResource<OwnerResource>
            {
                Items = owners.Select(ToOwnerResource).ToList(),
                ContinuationToken = null,
            }
        );
    }

    /// <summary>
    /// Determines whether the caller holds an operation at a resource node.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <param name="node">The resource node the operation applies to.</param>
    /// <param name="operation">The operation to authorize.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> whose result is <c>true</c> when the caller holds the operation at the
    /// node; otherwise, <c>false</c>.
    /// </returns>
    // See ADR-1465.
    protected internal virtual async ValueTask<bool> HoldsAuthorityAsync(
        HttpContext httpContext,
        ResourceNode node,
        IAuthorizationRequirement operation,
        CancellationToken cancellationToken
    )
    {
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
            operation
        );
        return authorizationResult.Succeeded;
    }

    /// <summary>
    /// Processes an HTTP <c>GET</c> for the principals that hold one role at a resource node: authorizes the caller
    /// to read the node, then returns the assignments of the role and whether the caller may grant and revoke the
    /// role there. Responds <c>404</c> when the resource does not exist.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <param name="node">The resource node that the role is assigned at.</param>
    /// <param name="roleName">The name of the role whose assignments are listed.</param>
    /// <param name="resourceExistsAsync">Determines whether the resource of the node exists.</param>
    /// <param name="canChangeAsync">
    /// Determines whether the caller may grant and revoke the role at the node, which is the
    /// <see cref="RoleListResource.CanChange"/> value of the response.
    /// </param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> whose result is the <see cref="IResult"/> of the response.
    /// </returns>
    // See ADR-1465.
    protected internal virtual async ValueTask<IResult> ProcessListRoleAssignmentsAsync(
        HttpContext httpContext,
        ResourceNode node,
        string roleName,
        Func<IStoreManager, CancellationToken, ValueTask<bool>> resourceExistsAsync,
        Func<IStoreManager, CancellationToken, ValueTask<bool>> canChangeAsync,
        CancellationToken cancellationToken
    )
    {
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
            Operations.Read
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        if (!await resourceExistsAsync(storeManager, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var assignments = await ResourceOwnershipService.GetAssignmentsAsync(
            storeManager,
            node.ResourceType,
            node.ResourceId,
            roleName,
            cancellationToken
        );
        var canChange = await canChangeAsync(storeManager, cancellationToken);
        var items = assignments.Select(ToOwnerResource).ToList();
        var roleList = new RoleListResource { Items = items, CanChange = canChange };
        return TypedResults.Json(roleList);
    }

    /// <summary>
    /// Processes an HTTP <c>POST</c> that grants a principal ownership of a resource: authorizes the caller against the
    /// resource node, then adds the owner and records the grant. Responds <c>201</c> when the owner is added,
    /// <c>200</c> when the principal already owns the resource, and <c>404</c> when the resource does not exist.
    /// </summary>
    protected internal virtual async ValueTask<IResult> ProcessAddOwnerAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceType,
        string resourceId,
        AddOwnerRequest request,
        string ownersPath,
        Func<IStoreManager, CancellationToken, ValueTask<bool>> resourceExistsAsync,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, resourceType, resourceId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
            Operations.Update
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        if (string.IsNullOrEmpty(request.PrincipalId))
        {
            return TypedResults.Problem(
                detail: "The 'principalId' is required.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        if (!await resourceExistsAsync(storeManager, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var grant = await ResourceOwnershipService.AddOwnerAsync(
            storeManager,
            tenantId,
            resourceType,
            resourceId,
            request.PrincipalId,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        var ownerResource = ToOwnerResource(grant.Assignment);
        if (!grant.IsAdded)
        {
            return TypedResults.Ok(ownerResource);
        }

        await RecordRoleAssignmentChangedAsync(
            httpContext,
            grant.Assignment,
            ResourceChangeTypes.Created,
            cancellationToken
        );

        return TypedResults.Created($"{ownersPath}/{grant.Assignment.PrincipalId}", ownerResource);
    }

    /// <summary>
    /// Processes an HTTP <c>DELETE</c> that revokes a principal's ownership of a resource: authorizes the caller
    /// against the resource node, then removes the owner and records each removed assignment. Responds <c>409</c> when
    /// the removal would orphan the resource and <c>404</c> when the principal is not an owner.
    /// </summary>
    protected internal virtual async ValueTask<IResult> ProcessRemoveOwnerAsync(
        HttpContext httpContext,
        string tenantId,
        string resourceType,
        string resourceId,
        string principalId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, resourceType, resourceId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
            Operations.Update
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var removal = await ResourceOwnershipService.RemoveOwnerAsync(
            storeManager,
            resourceType,
            resourceId,
            principalId,
            cancellationToken
        );

        switch (removal.Result)
        {
            case OwnerRemovalResult.NotFound:
                return TypedResults.NotFound();

            case OwnerRemovalResult.LastOwnerForbidden:
                return TypedResults.Problem(
                    detail: "The resource must retain at least one owner.",
                    statusCode: StatusCodes.Status409Conflict
                );

            default:
                await storeManager.SaveChangesAsync(cancellationToken);
                foreach (var removed in removal.RemovedAssignments)
                {
                    await RecordRoleAssignmentChangedAsync(
                        httpContext,
                        removed,
                        ResourceChangeTypes.Deleted,
                        cancellationToken
                    );
                }

                return TypedResults.NoContent();
        }
    }
}
