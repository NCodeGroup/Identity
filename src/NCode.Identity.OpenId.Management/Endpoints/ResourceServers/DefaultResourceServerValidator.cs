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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

/// <summary>
/// Provides the default implementation of <see cref="IResourceServerValidator"/>.
/// </summary>
internal class DefaultResourceServerValidator(IAuthorizationService authorizationService)
    : DefaultResourceValidator(authorizationService),
        IResourceServerValidator
{
    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateCreateAsync(
        ClaimsPrincipal user,
        PersistedResourceServer resourceServer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            resourceServer,
            Operations.Create,
            "The caller is not authorized to create this resource server.",
            "Authentication is required to create this resource server."
        );
        if (error is not null)
        {
            return error;
        }

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
    public async ValueTask<ManagementError?> ValidateUpdateAsync(
        ClaimsPrincipal user,
        PersistedResourceServer resourceServer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        return await AuthorizeAsync(
            user,
            ResourceNode.For(
                resourceServer.TenantId,
                ResourceNodeTypes.ResourceServer,
                resourceServer.ResourceServerId
            ),
            Operations.Update,
            "The caller is not authorized to update this resource server.",
            "Authentication is required to update this resource server."
        );
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateDeleteAsync(
        ClaimsPrincipal user,
        PersistedResourceServer resourceServer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            ResourceNode.For(
                resourceServer.TenantId,
                ResourceNodeTypes.ResourceServer,
                resourceServer.ResourceServerId
            ),
            Operations.Delete,
            "The caller is not authorized to delete this resource server.",
            "Authentication is required to delete this resource server."
        );
        if (error is not null)
        {
            return error;
        }

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
                    "The resource server cannot be deleted while it is still referenced by a client grant.",
            };
        }

        return null;
    }
}
