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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

/// <summary>
/// Provides the default implementation of <see cref="IClientGrantValidator"/>.
/// </summary>
[PublicAPI]
internal class DefaultClientGrantValidator(IAuthorizationService authorizationService)
    : DefaultResourceValidator(authorizationService),
        IClientGrantValidator
{
    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateCreateAsync(
        ClaimsPrincipal user,
        PersistedClientGrant grant,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            grant,
            Operations.Create,
            "The caller is not authorized to create this client grant.",
            "Authentication is required to create this client grant."
        );
        if (error is not null)
        {
            return error;
        }

        var scopeError = await ValidateScopesAsync(
            grant.ResourceServerId,
            grant.Scopes,
            storeManager,
            cancellationToken
        );
        if (scopeError is not null)
        {
            return scopeError;
        }

        var grantStore = storeManager.GetStore<IClientGrantStore>();
        var existing = await grantStore.GetOrDefaultAsync(
            grant.ClientId,
            grant.ResourceServerId,
            cancellationToken
        );
        if (existing is not null)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "A grant for the specified client and resource server already exists.",
            };
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateUpdateAsync(
        ClaimsPrincipal user,
        PersistedClientGrant grant,
        IReadOnlyList<string> scopes,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            grant,
            Operations.Update,
            "The caller is not authorized to update this client grant.",
            "Authentication is required to update this client grant."
        );
        if (error is not null)
        {
            return error;
        }

        return await ValidateScopesAsync(
            grant.ResourceServerId,
            scopes,
            storeManager,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateDeleteAsync(
        ClaimsPrincipal user,
        PersistedClientGrant grant,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        return await AuthorizeAsync(
            user,
            grant,
            Operations.Delete,
            "The caller is not authorized to delete this client grant.",
            "Authentication is required to delete this client grant."
        );
    }

    private static async ValueTask<ManagementError?> ValidateScopesAsync(
        string resourceServerId,
        IReadOnlyList<string> scopes,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();
        var resourceServer = await resourceServerStore.GetOrDefaultAsync(
            resourceServerId,
            cancellationToken
        );
        if (resourceServer is null)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status404NotFound,
                Detail = "The specified resource server does not exist.",
            };
        }

        var known = resourceServer
            .Scopes.Select(scope => scope.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalidScopes = scopes.Where(scope => !known.Contains(scope)).ToList();
        if (invalidScopes.Count > 0)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
                Detail =
                    $"The following scopes are not defined on the resource server: {string.Join(", ", invalidScopes)}.",
            };
        }

        return null;
    }
}
