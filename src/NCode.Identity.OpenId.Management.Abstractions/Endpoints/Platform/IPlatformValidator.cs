// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Management.Contracts.Platform;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Platform;

/// <summary>
/// Validates the intrinsic core preconditions for OpenID Server management operations. Register a custom
/// implementation to replace or extend the default validation.
/// </summary>
[PublicAPI]
public interface IPlatformValidator
{
    /// <summary>
    /// Validates that the specified OpenID Server may be created by the caller.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="server">The <see cref="PersistedPlatform"/> to be created.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateCreateAsync(
        ClaimsPrincipal user,
        PersistedPlatform server,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified OpenID Server may be updated by the caller to the supplied proposed state.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="server">The current <see cref="PersistedPlatform"/> being updated.</param>
    /// <param name="model">The proposed (patched) server metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateUpdateAsync(
        ClaimsPrincipal user,
        PersistedPlatform server,
        UpdatePlatformRequest model,
        string? ifMatch,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified OpenID Server may be deleted by the caller.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="server">The <see cref="PersistedPlatform"/> to be deleted.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateDeleteAsync(
        ClaimsPrincipal user,
        PersistedPlatform server,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
