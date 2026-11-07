// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.PrincipalResolution;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The management authentication endpoint filter. It runs after <see cref="ManagementEnvironmentEndpointFilter"/> has
/// materialized the OpenID request environment, validates a self-issued bearer access token for the management audience
/// against the control-plane tenant, and publishes the authenticated principal on <see cref="HttpContext.User"/> for the
/// endpoints' resource-based authorization to read. A missing or invalid token leaves the caller anonymous, and the
/// imperative authorization returns <c>401</c>/<c>403</c>. Once the token authenticates, the filter resolves the
/// client the caller acts as before the endpoint changes anything, and the resolver gives that answer for the rest of
/// the request, so a change the request commits, such as a machine client deleting itself, does not alter the actor
/// its audit records.
/// </summary>
// See ADR-1470.
internal sealed class ManagementAuthenticationEndpointFilter(
    IClientPrincipalResolver clientPrincipalResolver
) : IEndpointFilter
{
    private const string BearerPrefix = "Bearer ";
    private const string DPoPPrefix = "DPoP ";

    private static readonly string[] ManagementAudiences =
    [
        OpenIdConstants.SystemResourceServerIdentifiers.Management,
    ];

    private IClientPrincipalResolver ClientPrincipalResolver { get; } = clientPrincipalResolver;

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

                // The call is kept for the answer the request remembers, which the audit recorder reads after the
                // commit, so a change the request commits, such as a machine client deleting itself, does not alter
                // the actor its audit records.
                // See ADR-1470.
                _ = await ClientPrincipalResolver.GetActingClientOrDefaultAsync(
                    openIdContext,
                    disposition.Principal,
                    httpContext.RequestAborted
                );
            }
        }

        return await next(context);
    }

    private static bool TryGetBearerToken(HttpContext httpContext, out string token)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();

        // Accept both the Bearer scheme and the DPoP scheme (RFC 9449 §7.1).
        foreach (var prefix in (ReadOnlySpan<string>)[BearerPrefix, DPoPPrefix])
        {
            if (authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                token = authorization[prefix.Length..].Trim();
                return token.Length > 0;
            }
        }

        token = string.Empty;
        return false;
    }
}
