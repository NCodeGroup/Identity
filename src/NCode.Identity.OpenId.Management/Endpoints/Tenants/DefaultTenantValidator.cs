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
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/// <summary>
/// Provides the default implementation of <see cref="ITenantValidator"/>. A tenant's identifier and issuer are judged by
/// <see cref="ITenantResolver.ValidateTenantAsync"/>, the same rules the configured tenant strategy routes requests by,
/// so a tenant can never be written with an address nothing is served under.
/// </summary>
// See ADR-0160 (a tenant's address is validated wherever it is written).
internal class DefaultTenantValidator(
    IAuthorizationService authorizationService,
    ITenantResolver tenantResolver
) : DefaultResourceValidator(authorizationService), ITenantValidator
{
    private ITenantResolver TenantResolver { get; } = tenantResolver;

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

        var failures = await TenantResolver.ValidateTenantAsync(
            tenant.TenantId,
            tenant.Issuer,
            storeManager,
            cancellationToken
        );

        var invalidError = ToInvalidError(failures);
        if (invalidError is not null)
        {
            return invalidError;
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

        return ToConflictError(failures);
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
            ResourceNode.For(tenant.TenantId, ResourceNodeTypes.Tenant, tenant.TenantId),
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

        // The address is judged only when the issuer changes, so a tenant written under an earlier configuration can
        // still have its other metadata edited.
        if (
            string.Equals(
                model.Issuer ?? string.Empty,
                tenant.Issuer ?? string.Empty,
                StringComparison.Ordinal
            )
        )
        {
            return null;
        }

        var failures = await TenantResolver.ValidateTenantAsync(
            tenant.TenantId,
            model.Issuer,
            storeManager,
            cancellationToken
        );

        return ToInvalidError(failures) ?? ToConflictError(failures);
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
            ResourceNode.For(tenant.TenantId, ResourceNodeTypes.Tenant, tenant.TenantId),
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

    private static ManagementError? ToInvalidError(
        IReadOnlyList<TenantValidationFailure> failures
    ) => ToError(failures, isConflict: false, StatusCodes.Status400BadRequest);

    private static ManagementError? ToConflictError(
        IReadOnlyList<TenantValidationFailure> failures
    ) => ToError(failures, isConflict: true, StatusCodes.Status409Conflict);

    private static ManagementError? ToError(
        IReadOnlyList<TenantValidationFailure> failures,
        bool isConflict,
        int statusCode
    )
    {
        var details = failures
            .Where(failure => failure.IsConflict == isConflict)
            .Select(failure => failure.Detail)
            .ToList();

        return details.Count == 0
            ? null
            : new ManagementError { StatusCode = statusCode, Detail = string.Join(" ", details) };
    }
}
