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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The management authentication endpoint filter. It runs after <see cref="ManagementEnvironmentEndpointFilter"/> has
/// materialized the OpenID request environment, validates a self-issued bearer access token for the management audience
/// against the resolved tenant, and publishes the authenticated principal on <see cref="HttpContext.User"/> for the
/// endpoints' resource-based authorization to read. A missing or invalid token leaves the caller anonymous, and the
/// imperative authorization returns <c>401</c>/<c>403</c>.
/// </summary>
internal sealed class ManagementAuthenticationEndpointFilter : IEndpointFilter
{
    private const string BearerPrefix = "Bearer ";

    private static readonly string[] ManagementAudiences =
    [
        OpenIdConstants.SystemResourceServerIdentifiers.Management,
    ];

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;
        var openIdContext = httpContext.GetOpenIdContextOrDefault();

        if (openIdContext is not null && TryGetBearerToken(httpContext, out var accessToken))
        {
            var disposition = await openIdContext.Mediator.SendAsync<
                AuthenticateAccessTokenCommand,
                AuthenticateAccessTokenDisposition
            >(
                new AuthenticateAccessTokenCommand(openIdContext, accessToken, ManagementAudiences),
                httpContext.RequestAborted
            );

            if (disposition.IsAuthenticated)
            {
                httpContext.User = disposition.Principal;
            }
        }

        return await next(context);
    }

    private static bool TryGetBearerToken(HttpContext httpContext, out string token)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = authorization[BearerPrefix.Length..].Trim();
            return token.Length > 0;
        }

        token = string.Empty;
        return false;
    }
}
