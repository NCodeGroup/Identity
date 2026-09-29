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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/// <summary>
/// Validates that the caller is authorized to update the OpenID Tenant.
/// </summary>
[PublicAPI]
internal class DefaultAuthorizeUpdateTenantHandler(IAuthorizationService authorizationService)
    : ICommandHandler<ValidateUpdateTenantCommand>,
        ISupportMediatorPriority
{
    private IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateUpdateTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var (httpContext, tenant, _, disposition) = command;

        // short-circuit if an earlier precondition already failed
        if (disposition.HasError)
        {
            return;
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            tenant,
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;
            disposition.Error ??= new ManagementError
            {
                StatusCode = isAuthenticated
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized,
                Detail = isAuthenticated
                    ? "The caller is not authorized to update this tenant."
                    : "Authentication is required to update this tenant.",
            };
        }
    }
}
