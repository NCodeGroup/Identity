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
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Validates the intrinsic core preconditions for OpenID Client management operations. Register a custom
/// implementation to replace or extend the default validation.
/// </summary>
[PublicAPI]
public interface IClientValidator
{
    /// <summary>
    /// Validates that the specified OpenID Client may be created by the caller.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="client">The <see cref="PersistedClient"/> to be created.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateCreateAsync(
        ClaimsPrincipal user,
        PersistedClient client,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified OpenID Client may be updated by the caller.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="client">The current <see cref="PersistedClient"/> being updated.</param>
    /// <param name="model">The proposed (patched) client metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateUpdateAsync(
        ClaimsPrincipal user,
        PersistedClient client,
        UpdateClientRequest model,
        string? ifMatch,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified OpenID Client may be deleted by the caller.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="client">The <see cref="PersistedClient"/> to be deleted.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateDeleteAsync(
        ClaimsPrincipal user,
        PersistedClient client,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
