// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Validates the intrinsic core preconditions for OpenID Client management operations. The <c>Authorize*</c> members
/// judge the caller from the route values before any row is read, and the <c>Validate*</c> members judge the row. Register
/// a custom implementation to replace or extend the default validation.
/// </summary>
[PublicAPI]
public interface IClientValidator
{
    /// <summary>
    /// Authorizes the caller to create an OpenID Client in the specified tenant, and to update the resource server the
    /// client acts for when one is named, before any row is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant that receives the client.</param>
    /// <param name="resourceServerId">The identifier of the resource server the client acts for, or <c>null</c> when the client acts for none.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the refusal, or <c>null</c> when the caller is authorized.</returns>
    ValueTask<ManagementError?> AuthorizeCreateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string? resourceServerId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to create a machine client of the node that owns it, from the route values alone, before
    /// any row is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="controlPlaneTenantId">The identifier of the control-plane OpenID Tenant that receives the client.</param>
    /// <param name="owningNodeType">The resource type of the node that owns the machine client.</param>
    /// <param name="owningNodeId">The resource identifier of the node that owns the machine client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the refusal, or <c>null</c> when the caller is authorized.</returns>
    ValueTask<ManagementError?> AuthorizeCreateMachineClientAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        string owningNodeType,
        string owningNodeId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to update the specified OpenID Client before any row is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the refusal, or <c>null</c> when the caller is authorized.</returns>
    ValueTask<ManagementError?> AuthorizeUpdateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string clientId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to delete the specified OpenID Client before any row is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the refusal, or <c>null</c> when the caller is authorized.</returns>
    ValueTask<ManagementError?> AuthorizeDeleteAsync(
        ClaimsPrincipal user,
        string tenantId,
        string clientId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified OpenID Client may be created.
    /// </summary>
    /// <param name="client">The <see cref="PersistedClient"/> to be created.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateCreateAsync(
        PersistedClient client,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified OpenID Client may be updated.
    /// </summary>
    /// <param name="client">The current <see cref="PersistedClient"/> being updated.</param>
    /// <param name="model">The proposed (patched) client metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateUpdateAsync(
        PersistedClient client,
        UpdateClientRequest model,
        string? ifMatch,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified OpenID Client may be deleted.
    /// </summary>
    /// <param name="client">The <see cref="PersistedClient"/> to be deleted.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateDeleteAsync(
        PersistedClient client,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
