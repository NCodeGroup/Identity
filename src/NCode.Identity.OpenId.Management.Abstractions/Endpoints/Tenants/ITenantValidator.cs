// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/// <summary>
/// Validates the intrinsic core preconditions for OpenID Tenant management operations. Register a custom
/// implementation to replace or extend the default validation.
/// </summary>
[PublicAPI]
public interface ITenantValidator
{
    /// <summary>
    /// Authorizes the caller to create a tenant under the specified team. It judges only route values and runs before
    /// the team is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="controlPlaneTenantId">The identifier of the control-plane tenant that carries the team's authority node.</param>
    /// <param name="teamId">The identifier of the team named by the route.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> (<c>401</c> or <c>403</c>) when the caller lacks the authority, or <c>null</c> when authorized.</returns>
    ValueTask<ManagementError?> AuthorizeCreateAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        string teamId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to update the specified tenant. It judges only route values and runs before the tenant is
    /// read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the tenant named by the route.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> (<c>401</c> or <c>403</c>) when the caller lacks the authority, or <c>null</c> when authorized.</returns>
    ValueTask<ManagementError?> AuthorizeUpdateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to delete the specified tenant. It judges only route values and runs before the tenant is
    /// read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="tenantId">The identifier of the tenant named by the route.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> (<c>401</c> or <c>403</c>) when the caller lacks the authority, or <c>null</c> when authorized.</returns>
    ValueTask<ManagementError?> AuthorizeDeleteAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates the row of the tenant to be created. It does not authorize the route's node.
    /// </summary>
    /// <param name="tenant">The <see cref="PersistedTenant"/> to be created.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateCreateAsync(
        PersistedTenant tenant,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified OpenID Tenant may be updated to the supplied proposed state. It does not authorize
    /// the route's node.
    /// </summary>
    /// <param name="tenant">The current <see cref="PersistedTenant"/> being updated.</param>
    /// <param name="model">The proposed (patched) tenant metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> precondition: a quoted strong entity tag, <c>*</c>, or the bare concurrency token.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateUpdateAsync(
        PersistedTenant tenant,
        UpdateTenantRequest model,
        string? ifMatch,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified OpenID Tenant may be deleted. It does not authorize the route's node.
    /// </summary>
    /// <param name="tenant">The <see cref="PersistedTenant"/> to be deleted.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateDeleteAsync(
        PersistedTenant tenant,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
