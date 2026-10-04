#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    /// Processes an HTTP <c>POST</c> that grants a principal ownership of a resource: authorizes the caller against the
    /// resource node, then adds the owner (idempotently). Responds <c>404</c> when the resource does not exist.
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

        var owner = await ResourceOwnershipService.AddOwnerAsync(
            storeManager,
            tenantId,
            resourceType,
            resourceId,
            request.PrincipalId,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"{ownersPath}/{owner.PrincipalId}", ToOwnerResource(owner));
    }

    /// <summary>
    /// Processes an HTTP <c>DELETE</c> that revokes a principal's ownership of a resource: authorizes the caller
    /// against the resource node, then removes the owner. Responds <c>409</c> when the removal would orphan the
    /// resource and <c>404</c> when the principal is not an owner.
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
        var result = await ResourceOwnershipService.RemoveOwnerAsync(
            storeManager,
            resourceType,
            resourceId,
            principalId,
            cancellationToken
        );

        switch (result)
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
                return TypedResults.NoContent();
        }
    }
}
