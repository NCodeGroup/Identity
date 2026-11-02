// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Provides shared functionality for the default management validators, including resource-based authorization
/// mapped to a <see cref="ManagementError"/>. Public so capability packages that plug into the management slice (for
/// example, local accounts) can derive their own validators.
/// </summary>
[PublicAPI]
public abstract class DefaultResourceValidator(IAuthorizationService authorizationService)
{
    private IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <summary>
    /// Authorizes the caller against the specified resource and operation, returning a <see cref="ManagementError"/>
    /// (<c>403</c> when authenticated, <c>401</c> otherwise) on failure, or <c>null</c> on success.
    /// </summary>
    protected async ValueTask<ManagementError?> AuthorizeAsync(
        ClaimsPrincipal user,
        object resource,
        IAuthorizationRequirement operation,
        string forbiddenDetail,
        string unauthenticatedDetail
    )
    {
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            user,
            resource,
            operation
        );
        if (authorizationResult.Succeeded)
        {
            return null;
        }

        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;
        return new ManagementError
        {
            StatusCode = isAuthenticated
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized,
            Detail = isAuthenticated ? forbiddenDetail : unauthenticatedDetail,
        };
    }
}
