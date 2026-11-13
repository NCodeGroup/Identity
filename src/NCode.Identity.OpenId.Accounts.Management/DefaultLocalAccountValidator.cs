// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Endpoints;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.Management;

/// <summary>
/// Provides the default implementation of <see cref="ILocalAccountValidator"/>.
/// </summary>
internal class DefaultLocalAccountValidator(IAuthorizationService authorizationService)
    : DefaultResourceValidator(authorizationService),
        ILocalAccountValidator
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
            "The caller is not authorized to create local accounts in this tenant.",
            "Authentication is required to create local accounts in this tenant.",
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
            "The caller is not authorized to update this local account.",
            "Authentication is required to update this local account.",
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
            "The caller is not authorized to delete this local account.",
            "Authentication is required to delete this local account.",
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<ManagementError?> ValidateCreateAsync(
        string userName,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    ) =>
        CheckUserNameAvailableAsync(
            storeManager,
            userName,
            localAccountId: null,
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<ManagementError?> ValidateUpdateAsync(
        string localAccountId,
        string userName,
        string concurrencyToken,
        string? ifMatch,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        if (
            ManagementEntityTags.GetIfMatchErrorOrDefault(
                ifMatch,
                concurrencyToken,
                "local account"
            ) is
            { } preconditionError
        )
        {
            return ValueTask.FromResult<ManagementError?>(preconditionError);
        }

        return CheckUserNameAvailableAsync(
            storeManager,
            userName,
            localAccountId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public ValueTask<ManagementError?> ValidateDeleteAsync(
        string localAccountId,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult<ManagementError?>(null);

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
