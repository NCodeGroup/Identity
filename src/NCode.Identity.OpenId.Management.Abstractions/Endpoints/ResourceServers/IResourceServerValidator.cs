// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

/// <summary>
/// Validates the intrinsic core preconditions for resource server (API, in Auth0 terms) management operations.
/// The <c>Authorize*</c> members judge the caller from the route values before any row is read, and the
/// <c>Validate*</c> members judge the row. Register a custom implementation to replace or extend the default validation.
/// </summary>
[PublicAPI]
public interface IResourceServerValidator
{
    /// <summary>
    /// Authorizes the caller to create a resource server in the specified tenant before any row is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant that receives the resource server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the refusal, or <c>null</c> when the caller is authorized.</returns>
    ValueTask<ManagementError?> AuthorizeCreateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to update the specified resource server before any row is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="resourceServerId">The identifier of the resource server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the refusal, or <c>null</c> when the caller is authorized.</returns>
    ValueTask<ManagementError?> AuthorizeUpdateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string resourceServerId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to delete the specified resource server before any row is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="resourceServerId">The identifier of the resource server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the refusal, or <c>null</c> when the caller is authorized.</returns>
    ValueTask<ManagementError?> AuthorizeDeleteAsync(
        ClaimsPrincipal user,
        string tenantId,
        string resourceServerId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified resource server may be created.
    /// </summary>
    /// <param name="resourceServer">The <see cref="PersistedResourceServer"/> to be created.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateCreateAsync(
        PersistedResourceServer resourceServer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified resource server may be updated.
    /// </summary>
    /// <param name="resourceServer">The current <see cref="PersistedResourceServer"/> being updated.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateUpdateAsync(
        PersistedResourceServer resourceServer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified resource server may be deleted. A system resource server, or one
    /// still referenced by a client grant, cannot be deleted.
    /// </summary>
    /// <param name="resourceServer">The <see cref="PersistedResourceServer"/> to be deleted.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateDeleteAsync(
        PersistedResourceServer resourceServer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
