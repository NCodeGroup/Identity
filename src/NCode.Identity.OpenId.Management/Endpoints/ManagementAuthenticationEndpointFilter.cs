// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.PrincipalResolution;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The management authentication endpoint filter. It runs after <see cref="ManagementEnvironmentEndpointFilter"/> has
/// materialized the OpenID request environment, validates a self-issued bearer access token for the management audience
/// against the control-plane tenant, and publishes, on every management request, the principal a valid management access
/// token authenticates on <see cref="HttpContext.User"/> and as <see cref="IManagementCallerFeature"/>, for the
/// endpoints' resource-based authorization to read. Any other request is anonymous whatever principal an authentication
/// scheme set, and carries no feature; the imperative authorization returns <c>401</c>/<c>403</c>. Once the token
/// authenticates, the filter resolves the client the caller acts as before the endpoint changes anything, and the
/// resolver gives that answer for the rest of the request, so a change the request commits, such as a machine client
/// deleting itself, does not alter the actor its audit records.
/// </summary>
// See ADR-1470 and ADR-1484.
internal sealed class ManagementAuthenticationEndpointFilter(
    IClientPrincipalResolver clientPrincipalResolver
) : IEndpointFilter
{
    private const string BearerPrefix = "Bearer ";
    private const string DPoPPrefix = "DPoP ";

    private static string[] ManagementAudiences { get; } =
    [OpenIdConstants.SystemResourceServerIdentifiers.Management];

    private IClientPrincipalResolver ClientPrincipalResolver { get; } = clientPrincipalResolver;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;

        var caller = await AuthenticateOrDefaultAsync(httpContext, httpContext.RequestAborted);
        if (caller is null)
        {
            var anonymousIdentity = new ClaimsIdentity();
            httpContext.User = new ClaimsPrincipal(anonymousIdentity);
            httpContext.Features.Set<IManagementCallerFeature>(null);
        }
        else
        {
            httpContext.User = caller;
            var callerFeature = new DefaultManagementCallerFeature(caller);
            httpContext.Features.Set<IManagementCallerFeature>(callerFeature);
        }

        return await next(context);
    }

    private async ValueTask<ClaimsPrincipal?> AuthenticateOrDefaultAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContextOrDefault();
        if (openIdContext is null || !TryGetBearerToken(httpContext, out var accessToken))
            return null;

        var command = new AuthenticateAccessTokenCommand(
            openIdContext,
            accessToken,
            ManagementAudiences
        );
        var disposition = await openIdContext.Mediator.SendAsync<
            AuthenticateAccessTokenCommand,
            AuthenticateAccessTokenDisposition
        >(command, cancellationToken);

        if (!disposition.IsAuthenticated)
            return null;

        // The call is kept for the answer the request remembers, which the audit recorder reads after the
        // commit, so a change the request commits, such as a machine client deleting itself, does not alter
        // the actor its audit records.
        // See ADR-1470.
        _ = await ClientPrincipalResolver.GetActingClientOrDefaultAsync(
            openIdContext,
            disposition.Principal,
            cancellationToken
        );

        return disposition.Principal;
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
