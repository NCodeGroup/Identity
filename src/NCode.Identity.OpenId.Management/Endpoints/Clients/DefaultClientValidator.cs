// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientValidator"/>.
/// </summary>
internal class DefaultClientValidator(IAuthorizationService authorizationService)
    : DefaultResourceValidator(authorizationService),
        IClientValidator
{
    /// <inheritdoc />
    // Creating a client is authority at its tenant's node. A client that acts for a resource server receives the access
    // tokens issued for it at introspection, so binding one is a change to the resource server: the caller must also be
    // allowed to update it. The resource server's node is built from the request, before any row is read.
    public async ValueTask<ManagementError?> AuthorizeCreateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string? resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId),
            Operations.Create,
            "The caller is not authorized to create this client.",
            "Authentication is required to create this client.",
            cancellationToken
        );
        if (error is not null || resourceServerId is null)
        {
            return error;
        }

        return await AuthorizeAsync(
            user,
            ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId),
            Operations.Update,
            "The caller is not authorized to create a client for this resource server.",
            "Authentication is required to create a client for this resource server.",
            cancellationToken
        );
    }

    /// <inheritdoc />
    // See ADR-1466.
    public ValueTask<ManagementError?> AuthorizeCreateMachineClientAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        string owningNodeType,
        string owningNodeId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            ResourceNode.For(controlPlaneTenantId, owningNodeType, owningNodeId),
            Operations.Create,
            "The caller is not authorized to create a machine client of this owner.",
            "Authentication is required to create a machine client.",
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeUpdateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string clientId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId),
            Operations.Update,
            "The caller is not authorized to update this client.",
            "Authentication is required to update this client.",
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeDeleteAsync(
        ClaimsPrincipal user,
        string tenantId,
        string clientId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            ResourceNode.For(tenantId, ResourceNodeTypes.Client, clientId),
            Operations.Delete,
            "The caller is not authorized to delete this client.",
            "Authentication is required to delete this client.",
            cancellationToken
        );

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateCreateAsync(
        PersistedClient client,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var clientStore = storeManager.GetStore<IClientStore>();
        var existing = await clientStore.GetOrDefaultAsync(client.ClientId, cancellationToken);
        if (existing is not null)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "A client with the specified identifier already exists.",
            };
        }

        return await ValidateResourceServerBindingAsync(client, storeManager, cancellationToken);
    }

    // The resource server a client acts for must exist in the client's tenant. A system resource server is read by the
    // server itself and never acts through a client (ADR-1168).
    private static async ValueTask<ManagementError?> ValidateResourceServerBindingAsync(
        PersistedClient client,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        if (client.ResourceServerId is not { } resourceServerId)
        {
            return null;
        }

        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();
        var resourceServer = await resourceServerStore.GetOrDefaultAsync(
            resourceServerId,
            cancellationToken
        );
        if (
            resourceServer is null
            || !string.Equals(resourceServer.TenantId, client.TenantId, StringComparison.Ordinal)
        )
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Detail = "The resource server the client acts for does not exist in the tenant.",
            };
        }

        if (resourceServer.IsSystem)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Detail = "A client cannot act for a system resource server.",
            };
        }

        return null;
    }

    /// <inheritdoc />
    public ValueTask<ManagementError?> ValidateUpdateAsync(
        PersistedClient client,
        UpdateClientRequest model,
        string? ifMatch,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, client.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return ValueTask.FromResult<ManagementError?>(
                new ManagementError
                {
                    StatusCode = StatusCodes.Status412PreconditionFailed,
                    Detail = "The supplied If-Match token does not match the current client.",
                }
            );
        }

        return ValueTask.FromResult<ManagementError?>(null);
    }

    /// <inheritdoc />
    public ValueTask<ManagementError?> ValidateDeleteAsync(
        PersistedClient client,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult<ManagementError?>(null);
}
