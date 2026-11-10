// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Authentication;

/// <summary>
/// Authenticates the requests to the endpoints the BFF guards from the BFF session cookie, and answers challenges and
/// forbids. A page navigation that reaches a <c>SameSite=Strict</c> frontend without its session cookie is answered
/// once with the same-origin reload page that <see cref="BffPageReload"/> writes before the challenge redirects to
/// login.
/// </summary>
internal class BffAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    BffSessionResolver resolver,
    BffFrontendCatalog frontends,
    BffConnectionSessions connections,
    BffCookies cookies,
    BffPageReload pageReload
)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder),
        IAuthenticationSignOutHandler
{
    private BffSessionResolver Resolver { get; } = resolver;
    private BffFrontendCatalog Frontends { get; } = frontends;
    private BffConnectionSessions Connections { get; } = connections;
    private BffCookies Cookies { get; } = cookies;
    private BffPageReload PageReload { get; } = pageReload;

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var endpoint = Context.GetEndpoint();
        if (!BffGuardedEndpoints.IsGuarded(endpoint))
            return AuthenticateResult.NoResult();

        var session = await Resolver.ResolveAsync(Context, Context.RequestAborted);
        if (session is null)
            return AuthenticateResult.NoResult();

        var feature = Context.Features.Get<BffRequestFeature>();
        if (feature?.Principal is not { } principal)
            return AuthenticateResult.NoResult();

        if (BffGuardedEndpoints.IsHub(endpoint))
            principal = CreateConnectionPrincipal(principal, session);

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private ClaimsPrincipal CreateConnectionPrincipal(
        ClaimsPrincipal principal,
        BffSessionState session
    )
    {
        var logoutUrl =
            principal.FindFirst(BffClaimTypes.LogoutUrl)?.Value
            ?? throw new InvalidOperationException(
                "The BFF session principal has no logout URL claim."
            );
        var frontend = Frontends.GetRequired(Context);
        var connection = Connections.Create(frontend, session.Handle, logoutUrl);
        return new ClaimsPrincipal(
            new BffConnectionIdentity(principal.Identities.Single(), connection)
        );
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var endpoint = Context.GetEndpoint();
        if (
            BffGuardedEndpoints.RequiresAntiforgery(endpoint) || BffGuardedEndpoints.IsHub(endpoint)
        )
        {
            return BffResponses
                .WriteProblemAsync(
                    Context,
                    StatusCodes.Status401Unauthorized,
                    BffProblemTypes.SessionRequired,
                    "A BFF session is required.",
                    Context.RequestAborted
                )
                .AsTask();
        }

        if (!BffGuardedEndpoints.IsPage(endpoint))
        {
            throw new InvalidOperationException(
                "The BFF scheme challenged an endpoint it does not guard. Mark an API AsBffApi() or [BffApi], a hub AsBffHub() or [BffHub], or a page AsBffPage() or [BffPage]."
            );
        }

        var frontend = Frontends.Resolve(Context);
        if (frontend is null)
        {
            return BffResponses
                .WriteProblemAsync(
                    Context,
                    StatusCodes.Status401Unauthorized,
                    BffProblemTypes.SessionRequired,
                    "A BFF session is required.",
                    Context.RequestAborted
                )
                .AsTask();
        }

        var request = Request;
        if (PageReload.ShouldReload(Context, frontend))
            return PageReload.WriteAsync(Context, frontend, Context.RequestAborted).AsTask();

        if (Cookies.HasPageReload(request, frontend))
            Cookies.DeletePageReload(Response, frontend);

        var returnUrl = $"{request.Path}{request.QueryString}";
        if (!BffReturnUrls.IsLocalUrl(returnUrl, frontend.Options.ReturnUrls))
            returnUrl = "/";

        var loginUrl = BffNavigation.LocalUrl(
            request,
            frontend.Options.PathPrefix + BffEndpointPaths.Login
        );
        var escaped = Uri.EscapeDataString(returnUrl);
        Response.Redirect($"{loginUrl}?returnUrl={escaped}");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SignOutAsync(AuthenticationProperties? properties)
    {
        if (!BffGuardedEndpoints.IsApi(Context.GetEndpoint()))
        {
            throw new InvalidOperationException(
                "SignOutAsync with the BFF scheme ends a session only on an endpoint marked AsBffApi() or [BffApi], whose anti-forgery checks keep another site from ending it; a page posts a form to /bff/logout."
            );
        }

        var session = await Resolver.ResolveAsync(Context, Context.RequestAborted);
        if (session is null)
            return;

        var logout = Context.RequestServices.GetRequiredService<BffLogoutService>();
        await logout.EndSessionAsync(Context, session, BffSessionEndReasons.LoggedOut);
    }

    /// <inheritdoc />
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        var endpoint = Context.GetEndpoint();
        if (
            !BffGuardedEndpoints.RequiresAntiforgery(endpoint)
            && !BffGuardedEndpoints.IsHub(endpoint)
        )
            return base.HandleForbiddenAsync(properties);

        return BffResponses
            .WriteProblemAsync(
                Context,
                StatusCodes.Status403Forbidden,
                BffProblemTypes.Forbidden,
                "Access to this resource is forbidden.",
                Context.RequestAborted
            )
            .AsTask();
    }
}
