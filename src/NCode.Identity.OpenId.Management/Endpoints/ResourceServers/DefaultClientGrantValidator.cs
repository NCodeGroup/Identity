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
/// Provides the default implementation of <see cref="IClientGrantValidator"/>.
/// </summary>
internal class DefaultClientGrantValidator(IAuthorizationService authorizationService)
    : DefaultResourceValidator(authorizationService),
        IClientGrantValidator
{
    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeCreateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            TenantScopeResource.For(tenantId),
            Operations.Create,
            "The caller is not authorized to create this client grant.",
            "Authentication is required to create this client grant.",
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeUpdateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            TenantScopeResource.For(tenantId),
            Operations.Update,
            "The caller is not authorized to update this client grant.",
            "Authentication is required to update this client grant.",
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeDeleteAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            TenantScopeResource.For(tenantId),
            Operations.Delete,
            "The caller is not authorized to delete this client grant.",
            "Authentication is required to delete this client grant.",
            cancellationToken
        );

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateCreateAsync(
        PersistedClientGrant grant,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
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
        PersistedClientGrant grant,
        IReadOnlyList<string> scopes,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        return await ValidateScopesAsync(
            grant.ResourceServerId,
            scopes,
            storeManager,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public ValueTask<ManagementError?> ValidateDeleteAsync(
        PersistedClientGrant grant,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult<ManagementError?>(null);

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
