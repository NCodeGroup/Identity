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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/// <summary>
/// Provides the default implementation of <see cref="ITenantValidator"/>.
/// </summary>
[PublicAPI]
internal class DefaultTenantValidator(IAuthorizationService authorizationService)
    : DefaultResourceValidator(authorizationService),
        ITenantValidator
{
    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateCreateAsync(
        ClaimsPrincipal user,
        PersistedTenant tenant,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            tenant,
            Operations.Create,
            "The caller is not authorized to create this tenant.",
            "Authentication is required to create this tenant."
        );
        if (error is not null)
        {
            return error;
        }

        var store = storeManager.GetStore<ITenantStore>();

        var existing = await store.GetOrDefaultAsync(tenant.TenantId, cancellationToken);
        if (existing is not null)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "A tenant with the specified identifier already exists.",
            };
        }

        // the domain name is optional; the unique constraint only applies when one is supplied
        if (!string.IsNullOrEmpty(tenant.DomainName))
        {
            var existingByDomain = await store.GetOrDefaultByDomainNameAsync(
                tenant.DomainName,
                cancellationToken
            );
            if (existingByDomain is not null)
            {
                return new ManagementError
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Detail = "A tenant with the specified domain name already exists.",
                };
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateUpdateAsync(
        ClaimsPrincipal user,
        PersistedTenant tenant,
        UpdateTenantRequest model,
        string? ifMatch,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            tenant,
            Operations.Update,
            "The caller is not authorized to update this tenant.",
            "Authentication is required to update this tenant."
        );
        if (error is not null)
        {
            return error;
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, tenant.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status412PreconditionFailed,
                Detail = "The supplied If-Match token does not match the current tenant.",
            };
        }

        if (string.IsNullOrEmpty(model.DisplayName))
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Detail = "The 'displayName' is required.",
            };
        }

        // check domain uniqueness only when it actually changed, excluding this same tenant
        if (
            !string.IsNullOrEmpty(model.DomainName)
            && !string.Equals(model.DomainName, tenant.DomainName, StringComparison.Ordinal)
        )
        {
            var store = storeManager.GetStore<ITenantStore>();
            var existingByDomain = await store.GetOrDefaultByDomainNameAsync(
                model.DomainName,
                cancellationToken
            );
            if (
                existingByDomain is not null
                && !string.Equals(
                    existingByDomain.TenantId,
                    tenant.TenantId,
                    StringComparison.Ordinal
                )
            )
            {
                return new ManagementError
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Detail = "A tenant with the specified domain name already exists.",
                };
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateDeleteAsync(
        ClaimsPrincipal user,
        PersistedTenant tenant,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            tenant,
            Operations.Delete,
            "The caller is not authorized to delete this tenant.",
            "Authentication is required to delete this tenant."
        );
        if (error is not null)
        {
            return error;
        }

        var store = storeManager.GetStore<ITenantStore>();
        var hasDependents = await store.HasDependentsAsync(tenant.TenantId, cancellationToken);
        if (hasDependents)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail =
                    "The tenant cannot be deleted while it still has dependent clients or secrets.",
            };
        }

        return null;
    }
}
