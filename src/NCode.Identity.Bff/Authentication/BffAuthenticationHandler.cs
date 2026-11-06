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
/// once with a same-origin reload page before the challenge redirects to login.
/// </summary>
internal class BffAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    BffSessionResolver resolver,
    BffFrontendCatalog frontends,
    BffConnectionSessions connections,
    BffCookies cookies
)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder),
        IAuthenticationSignOutHandler
{
    private const string LoginPath = "/login";

    private const string SecFetchModeHeaderName = "Sec-Fetch-Mode";
    private const string NavigateFetchMode = "navigate";
    private const string ReferrerPolicyHeaderName = "Referrer-Policy";
    private const string ReloadContentType = "text/html; charset=utf-8";
    private const string ReloadCacheControl = "no-store";
    private const string ReloadReferrerPolicy = "no-referrer";
    private const string ReloadNoSniff = "nosniff";
    private const string ReloadContentSecurityPolicy =
        "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
    private const string ReloadUrlPlaceholder = "{url}";
    private const string ReloadPageTemplate =
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"refresh\" content=\"0;url={url}\"><title>Continue</title></head><body><a href=\"{url}\">Continue</a></body></html>";

    private BffSessionResolver Resolver { get; } = resolver;
    private BffFrontendCatalog Frontends { get; } = frontends;
    private BffConnectionSessions Connections { get; } = connections;
    private BffCookies Cookies { get; } = cookies;

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
            return BffResponses.WriteProblemAsync(
                Context,
                StatusCodes.Status401Unauthorized,
                BffProblemTypes.SessionRequired,
                "A BFF session is required."
            );
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
            return BffResponses.WriteProblemAsync(
                Context,
                StatusCodes.Status401Unauthorized,
                BffProblemTypes.SessionRequired,
                "A BFF session is required."
            );
        }

        var request = Request;
        if (IsSameSiteReload(frontend))
            return WriteSameSiteReloadAsync(frontend, Context.RequestAborted).AsTask();

        if (Cookies.HasPageReload(request, frontend))
            Cookies.DeletePageReload(Response, frontend);

        var returnUrl = $"{request.Path}{request.QueryString}";
        if (!BffReturnUrls.IsLocalUrl(returnUrl, frontend.Options.ReturnUrls))
            returnUrl = "/";

        var loginUrl = BffNavigation.LocalUrl(request, frontend.Options.PathPrefix + LoginPath);
        var escaped = Uri.EscapeDataString(returnUrl);
        Response.Redirect($"{loginUrl}?returnUrl={escaped}");
        return Task.CompletedTask;
    }

    private bool IsSameSiteReload(BffFrontend frontend)
    {
        var request = Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;

        var fetchMode = request.Headers[SecFetchModeHeaderName].ToString();
        if (
            fetchMode.Length != 0
            && !string.Equals(fetchMode, NavigateFetchMode, StringComparison.OrdinalIgnoreCase)
        )
            return false;

        return frontend.Options.Session.SameSite == SameSiteMode.Strict
            && Cookies.ReadSessionHandle(request, frontend) is null
            && !Cookies.HasPageReload(request, frontend);
    }

    /// <summary>
    /// Answers the request with a same-origin reload page and sets the marker cookie that limits the reload to once.
    /// </summary>
    /// <remarks>
    /// A <c>SameSite=Strict</c> session cookie is withheld on a top-level navigation that a cross-site page or
    /// redirect chain starts, such as the redirect from the callback endpoint to the return URL, which a browser
    /// labels as <c>Sec-Fetch-Site: cross-site</c> or <c>none</c> depending on the browser, so Fetch Metadata cannot
    /// tell it apart. The page answers once with a reload page that points at the request's own URL, which the browser
    /// follows as a same-origin navigation that carries the cookie, and the <c>SameSite=Lax</c> marker cookie limits the
    /// reload to once. A <c>HEAD</c> request gets the headers and no body.
    /// </remarks>
    /// <param name="frontend">The frontend that the request belongs to.</param>
    /// <param name="cancellationToken">The token that cancels the write.</param>
    private async ValueTask WriteSameSiteReloadAsync(
        BffFrontend frontend,
        CancellationToken cancellationToken
    )
    {
        var request = Request;
        var response = Response;
        Cookies.SetPageReload(response, frontend);

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = ReloadContentType;
        response.Headers.CacheControl = ReloadCacheControl;
        response.Headers[ReferrerPolicyHeaderName] = ReloadReferrerPolicy;
        response.Headers.XContentTypeOptions = ReloadNoSniff;
        response.Headers.ContentSecurityPolicy = ReloadContentSecurityPolicy;

        if (HttpMethods.IsHead(request.Method))
            return;

        var url =
            $"{request.PathBase.ToUriComponent()}{request.Path.ToUriComponent()}{request.QueryString.ToUriComponent()}";
        var encoded = HtmlEncoder.Default.Encode(url);
        var body = ReloadPageTemplate.Replace(
            ReloadUrlPlaceholder,
            encoded,
            StringComparison.Ordinal
        );
        await response.WriteAsync(body, cancellationToken);
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

        return BffResponses.WriteProblemAsync(
            Context,
            StatusCodes.Status403Forbidden,
            BffProblemTypes.Forbidden,
            "Access to this resource is forbidden."
        );
    }
}
