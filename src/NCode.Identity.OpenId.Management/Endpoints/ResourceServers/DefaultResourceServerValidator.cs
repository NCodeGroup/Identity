// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

/// <summary>
/// Provides the default implementation of <see cref="IResourceServerValidator"/>.
/// </summary>
internal class DefaultResourceServerValidator(IAuthorizationService authorizationService)
    : DefaultResourceValidator(authorizationService),
        IResourceServerValidator
{
    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeCreateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId),
            Operations.Create,
            "The caller is not authorized to create this resource server.",
            "Authentication is required to create this resource server.",
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeUpdateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string resourceServerId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId),
            Operations.Update,
            "The caller is not authorized to update this resource server.",
            "Authentication is required to update this resource server.",
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeDeleteAsync(
        ClaimsPrincipal user,
        string tenantId,
        string resourceServerId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId),
            Operations.Delete,
            "The caller is not authorized to delete this resource server.",
            "Authentication is required to delete this resource server.",
            cancellationToken
        );

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateCreateAsync(
        PersistedResourceServer resourceServer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var store = storeManager.GetStore<IResourceServerStore>();
        var existing = await store.GetByIdentifierOrDefaultAsync(
            resourceServer.Identifier,
            cancellationToken
        );
        if (existing is not null)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "A resource server with the specified identifier already exists.",
            };
        }

        return null;
    }

    /// <inheritdoc />
    public ValueTask<ManagementError?> ValidateUpdateAsync(
        PersistedResourceServer resourceServer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult<ManagementError?>(null);

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateDeleteAsync(
        PersistedResourceServer resourceServer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        if (resourceServer.IsSystem)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "A reserved system resource server cannot be deleted.",
            };
        }

        var store = storeManager.GetStore<IResourceServerStore>();
        var hasDependents = await store.HasDependentsAsync(
            resourceServer.ResourceServerId,
            cancellationToken
        );
        if (hasDependents)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail =
                    "The resource server cannot be deleted while a client grant references it or a client acts for it.",
            };
        }

        return null;
    }
}
