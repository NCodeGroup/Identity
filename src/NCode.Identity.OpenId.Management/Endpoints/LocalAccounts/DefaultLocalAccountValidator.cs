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
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.LocalAccounts;

/// <summary>
/// Provides the default implementation of <see cref="ILocalAccountValidator"/>.
/// </summary>
internal class DefaultLocalAccountValidator(IAuthorizationService authorizationService)
    : DefaultResourceValidator(authorizationService),
        ILocalAccountValidator
{
    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateCreateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string userName,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            TenantScopeResource.For(tenantId),
            Operations.Create,
            "The caller is not authorized to create local accounts in this tenant.",
            "Authentication is required to create local accounts in this tenant."
        );
        if (error is not null)
        {
            return error;
        }

        return await CheckUserNameAvailableAsync(
            storeManager,
            userName,
            localAccountId: null,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateUpdateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string localAccountId,
        string userName,
        string concurrencyToken,
        string? ifMatch,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            TenantScopeResource.For(tenantId),
            Operations.Update,
            "The caller is not authorized to update this local account.",
            "Authentication is required to update this local account."
        );
        if (error is not null)
        {
            return error;
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, concurrencyToken, StringComparison.Ordinal)
        )
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status412PreconditionFailed,
                Detail = "The supplied If-Match token does not match the current local account.",
            };
        }

        return await CheckUserNameAvailableAsync(
            storeManager,
            userName,
            localAccountId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateDeleteAsync(
        ClaimsPrincipal user,
        string tenantId,
        string localAccountId,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        return await AuthorizeAsync(
            user,
            TenantScopeResource.For(tenantId),
            Operations.Delete,
            "The caller is not authorized to delete this local account.",
            "Authentication is required to delete this local account."
        );
    }

    // A username is unique within the tenant; a match against a different account (or any account, on create) conflicts.
    private static async ValueTask<ManagementError?> CheckUserNameAvailableAsync(
        IStoreManager storeManager,
        string userName,
        string? localAccountId,
        CancellationToken cancellationToken
    )
    {
        var existing = await storeManager
            .GetStore<ILocalAccountStore>()
            .GetByUserNameOrDefaultAsync(userName, cancellationToken);

        if (
            existing is not null
            && !string.Equals(existing.LocalAccountId, localAccountId, StringComparison.Ordinal)
        )
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "A local account with the specified username already exists.",
            };
        }

        return null;
    }
}
