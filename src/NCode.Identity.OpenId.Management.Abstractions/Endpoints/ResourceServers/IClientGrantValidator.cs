// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

/// <summary>
/// Validates the intrinsic core preconditions for client grant (in Auth0 terms) management operations: a client's
/// authorization to a resource server with a set of granted scopes. The <c>Authorize*</c> members judge the caller
/// from the route values before any row is read, and the <c>Validate*</c> members judge the row. Register a custom
/// implementation to replace or extend the default validation.
/// </summary>
[PublicAPI]
public interface IClientGrantValidator
{
    /// <summary>
    /// Authorizes the caller to create a client grant in the specified tenant before any row is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant that receives the client grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the refusal, or <c>null</c> when the caller is authorized.</returns>
    ValueTask<ManagementError?> AuthorizeCreateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to update a client grant in the specified tenant before any row is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the refusal, or <c>null</c> when the caller is authorized.</returns>
    ValueTask<ManagementError?> AuthorizeUpdateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to delete a client grant in the specified tenant before any row is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the refusal, or <c>null</c> when the caller is authorized.</returns>
    ValueTask<ManagementError?> AuthorizeDeleteAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified client grant may be created. The target resource server must exist and every
    /// granted scope must be defined on it.
    /// </summary>
    /// <param name="grant">The <see cref="PersistedClientGrant"/> to be created.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateCreateAsync(
        PersistedClientGrant grant,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified client grant may be updated with the proposed scopes. Every proposed scope must
    /// be defined on the target resource server.
    /// </summary>
    /// <param name="grant">The current <see cref="PersistedClientGrant"/> being updated.</param>
    /// <param name="scopes">The proposed replacement set of granted scope values.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateUpdateAsync(
        PersistedClientGrant grant,
        IReadOnlyList<string> scopes,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified client grant may be deleted.
    /// </summary>
    /// <param name="grant">The <see cref="PersistedClientGrant"/> to be deleted.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateDeleteAsync(
        PersistedClientGrant grant,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
