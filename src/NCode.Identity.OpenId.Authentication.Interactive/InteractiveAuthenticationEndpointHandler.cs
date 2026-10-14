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

using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Consent;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The <see cref="IEndpointProvider"/> for the interactive login/consent/account surface. It serves the single-page
/// bundle for the login, consent, and account ("me") pages, verifies credentials through the registered
/// <see cref="IInteractiveCredentialVerifier"/>, issues the cookie session on success, exposes the current session as
/// JSON, and clears it on logout. The login, consent, and logout forms submit as native form POSTs carrying the
/// antiforgery token, so the server-driven cookie challenge/redirect flow is preserved.
/// </summary>
internal sealed class InteractiveAuthenticationEndpointHandler(
    IInteractiveCredentialVerifier credentialVerifier,
    IConsentService consentService,
    ISessionService sessionService,
    TimeProvider timeProvider,
    IAntiforgery antiforgery,
    InteractiveAuthenticationOptions options
) : IEndpointProvider
{
    private IInteractiveCredentialVerifier CredentialVerifier { get; } = credentialVerifier;
    private IConsentService ConsentService { get; } = consentService;
    private ISessionService SessionService { get; } = sessionService;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IAntiforgery Antiforgery { get; } = antiforgery;
    private InteractiveAuthenticationOptions Options { get; } = options;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(InteractiveAuthenticationDefaults.AccountPath, GetAccountPageAsync);
        endpoints.MapGet(InteractiveAuthenticationDefaults.MePath, GetMeAsync);
        endpoints.MapGet(InteractiveAuthenticationDefaults.LoginPath, GetLoginPageAsync);
        endpoints.MapPost(InteractiveAuthenticationDefaults.LoginPath, PostLoginAsync);
        endpoints.MapGet(InteractiveAuthenticationDefaults.ConsentPath, GetConsentPageAsync);
        endpoints.MapGet(
            InteractiveAuthenticationDefaults.ConsentDetailsPath,
            GetConsentDetailsAsync
        );
        endpoints.MapPost(InteractiveAuthenticationDefaults.ConsentPath, PostConsentAsync);
        endpoints.MapPost(InteractiveAuthenticationDefaults.LogoutPath, PostLogoutAsync);
        endpoints.MapGet(
            InteractiveAuthenticationDefaults.ApplicationsPath,
            GetApplicationsPageAsync
        );
        endpoints.MapGet(
            InteractiveAuthenticationDefaults.ApplicationsListPath,
            GetApplicationsAsync
        );
        endpoints.MapPost(
            InteractiveAuthenticationDefaults.ApplicationsRevokePath,
            PostRevokeApplicationAsync
        );
    }

    /// <summary>
    /// Serves the login page (the single-page bundle), establishing the antiforgery token the form echoes back.
    /// </summary>
    public async ValueTask<IResult> GetLoginPageAsync(HttpContext httpContext) =>
        await ServeBundleAsync(httpContext);

    /// <summary>
    /// Serves the consent page (the single-page bundle), establishing the antiforgery token the form echoes back.
    /// </summary>
    public async ValueTask<IResult> GetConsentPageAsync(HttpContext httpContext) =>
        await ServeBundleAsync(httpContext);

    /// <summary>
    /// Serves the account ("me") page (the single-page bundle), establishing the antiforgery token the sign-out form
    /// echoes back. An anonymous request is redirected to the login page with the account page as its return URL.
    /// </summary>
    public async ValueTask<IResult> GetAccountPageAsync(HttpContext httpContext)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(
            InteractiveAuthenticationDefaults.CookieSchemeName
        );
        if (!authenticateResult.Succeeded)
        {
            return TypedResults.Redirect(
                $"{InteractiveAuthenticationDefaults.LoginPath}?ReturnUrl="
                    + Uri.EscapeDataString(InteractiveAuthenticationDefaults.AccountPath)
            );
        }

        return await ServeBundleAsync(httpContext);
    }

    /// <summary>
    /// Returns the current end-user's session as JSON for the account page, or <c>401</c> when the request is not
    /// authenticated.
    /// </summary>
    public static async ValueTask<IResult> GetMeAsync(HttpContext httpContext)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(
            InteractiveAuthenticationDefaults.CookieSchemeName
        );
        if (!authenticateResult.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var principal = authenticateResult.Principal;
        var subjectId = openIdContext.GetSubjectId(principal);
        if (string.IsNullOrEmpty(subjectId))
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Json(BuildMeResponse(principal, subjectId));
    }

    // Projects the authenticated cookie principal into the account page's view model.
    internal static AccountMeResponse BuildMeResponse(ClaimsPrincipal principal, string subjectId)
    {
        var name =
            principal.FindFirst(JoseClaimNames.Payload.Name)?.Value
            ?? principal.FindFirst(JoseClaimNames.Payload.PreferredUsername)?.Value
            ?? subjectId;

        var amr = principal
            .FindAll(JoseClaimNames.Payload.Amr)
            .Select(claim => claim.Value)
            .ToList();

        long? authTime = long.TryParse(
            principal.FindFirst(JoseClaimNames.Payload.AuthTime)?.Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedAuthTime
        )
            ? parsedAuthTime
            : null;

        return new AccountMeResponse(
            subjectId,
            name,
            authTime,
            amr,
            principal.FindFirst(JoseClaimNames.Payload.Idp)?.Value,
            principal.FindFirst(JoseClaimNames.Payload.Sid)?.Value
        );
    }

    /// <summary>
    /// Verifies the posted credentials through the registered verifier and, on success, signs in the cookie session
    /// and redirects to the validated local return URL; on failure it redirects back to the login page with an error.
    /// </summary>
    public async ValueTask<IResult> PostLoginAsync(
        HttpContext httpContext,
        [FromForm] string? username,
        [FromForm] string? password,
        [FromForm(Name = "ReturnUrl")] string? returnUrl
    )
    {
        var safeReturnUrl = ResolveSafeReturnUrl(httpContext, returnUrl);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return TypedResults.Redirect(BuildLoginRedirect(safeReturnUrl, "invalid_request"));
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var principal = await CredentialVerifier.VerifyCredentialAsync(
            openIdContext,
            username,
            password,
            httpContext.RequestAborted
        );
        if (principal is null)
        {
            return TypedResults.Redirect(BuildLoginRedirect(safeReturnUrl, "invalid_credentials"));
        }

        // Establish a server-side session and mint its opaque `sid` onto the cookie principal so every subsequent
        // authorize request resolves the same session and the `sid` claim can be emitted into issued tokens.
        await EstablishSessionAsync(openIdContext, principal, httpContext.RequestAborted);

        await httpContext.SignInAsync(
            InteractiveAuthenticationDefaults.CookieSchemeName,
            principal
        );

        return TypedResults.Redirect(safeReturnUrl);
    }

    private async ValueTask EstablishSessionAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken
    )
    {
        // A re-authentication onto an already-session-bearing principal keeps its existing session.
        if (principal.HasClaim(claim => claim.Type == JoseClaimNames.Payload.Sid))
            return;

        var subjectId = openIdContext.GetSubjectId(principal);
        if (string.IsNullOrEmpty(subjectId))
            return;

        var identity = principal.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated);
        if (identity is null)
            return;

        var amr = principal
            .FindAll(JoseClaimNames.Payload.Amr)
            .Select(claim => claim.Value)
            .ToList();

        var request = new CreateSessionRequest
        {
            SubjectId = subjectId,
            AuthTime = ReadAuthTime(principal),
            AuthenticationMethods = amr.Count > 0 ? amr : null,
            AuthenticationContextClassReference = principal
                .FindFirst(JoseClaimNames.Payload.Acr)
                ?.Value,
            IdentityProvider = principal.FindFirst(JoseClaimNames.Payload.Idp)?.Value,
            Lifetime = Options.SessionLifetime,
        };

        var sessionId = await SessionService.CreateAsync(openIdContext, request, cancellationToken);
        identity.AddClaim(new Claim(JoseClaimNames.Payload.Sid, sessionId));
    }

    private DateTimeOffset ReadAuthTime(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(JoseClaimNames.Payload.AuthTime)?.Value;
        return long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var authTime
        )
            ? DateTimeOffset.FromUnixTimeSeconds(authTime)
            : TimeProvider.GetUtcNow();
    }

    /// <summary>
    /// Returns the pending authorization request (the client — with its friendly display name — and the requested
    /// scopes with friendly descriptions and already-granted state) the consent page must present, resolved — without
    /// consuming — from the parked continuation carried by the return URL. Returns <c>401</c> when the request is not
    /// authenticated and <c>404</c> when no pending request resolves.
    /// </summary>
    public async ValueTask<IResult> GetConsentDetailsAsync(
        HttpContext httpContext,
        [FromQuery(Name = "ReturnUrl")] string? returnUrl
    )
    {
        var authenticateResult = await httpContext.AuthenticateAsync(
            InteractiveAuthenticationDefaults.CookieSchemeName
        );
        if (!authenticateResult.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrEmpty(returnUrl))
        {
            return TypedResults.NotFound();
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var subjectId = openIdContext.GetSubjectId(authenticateResult.Principal);
        if (string.IsNullOrEmpty(subjectId))
        {
            return TypedResults.Unauthorized();
        }

        var pending = await ConsentService.ResolvePendingConsentAsync(
            openIdContext,
            subjectId,
            returnUrl,
            httpContext.RequestAborted
        );
        if (pending is not { } value)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Json(
            new ConsentDetailsResponse(
                value.ClientId,
                value.ClientName,
                value
                    .Scopes.Select(scope => new ConsentScopeResponse(
                        scope.Value,
                        scope.Description,
                        scope.IsGranted
                    ))
                    .ToList()
            )
        );
    }

    /// <summary>
    /// Records the granular consent outcome: an allow persists the subject's decision — the requested scopes (resolved
    /// authoritatively from the continuation, not the posted form) and the subset the subject selected (the posted
    /// <c>scope</c> fields, intersected with the requested scopes) — and returns to the original authorize URL to
    /// resume; a deny abandons the flow.
    /// </summary>
    public async ValueTask<IResult> PostConsentAsync(
        HttpContext httpContext,
        [FromForm] string? action,
        [FromForm(Name = "ReturnUrl")] string? returnUrl
    )
    {
        var safeReturnUrl = ResolveSafeReturnUrl(httpContext, returnUrl);

        if (string.Equals(action, "deny", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Redirect("/");
        }

        var authenticateResult = await httpContext.AuthenticateAsync(
            InteractiveAuthenticationDefaults.CookieSchemeName
        );
        if (!authenticateResult.Succeeded)
        {
            return TypedResults.Redirect(BuildLoginRedirect(safeReturnUrl, "invalid_request"));
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var subjectId = openIdContext.GetSubjectId(authenticateResult.Principal);
        if (string.IsNullOrEmpty(subjectId) || string.IsNullOrEmpty(returnUrl))
        {
            return TypedResults.Redirect(BuildLoginRedirect(safeReturnUrl, "invalid_request"));
        }

        var pending = await ConsentService.ResolvePendingConsentAsync(
            openIdContext,
            subjectId,
            returnUrl,
            httpContext.RequestAborted
        );
        if (pending is { } value)
        {
            var requestedScopes = value.Scopes.Select(scope => scope.Value).ToList();
            var grantedScopes = httpContext.Request.Form["scope"].OfType<string>().ToList();

            await ConsentService.GrantConsentAsync(
                openIdContext,
                value.ClientId,
                subjectId,
                requestedScopes,
                grantedScopes,
                httpContext.RequestAborted
            );
        }

        return TypedResults.Redirect(safeReturnUrl);
    }

    /// <summary>
    /// Ends the current end-user's session from the first-party account page. It runs the same server-side logout
    /// pipeline as the OpenID Connect <c>end_session_endpoint</c> — resolving the session from the login cookie,
    /// clearing the cookie, revoking the session and the subject's grants, and notifying participating relying parties
    /// via back-channel logout — then redirects to the validated local return URL (the login page by default).
    /// </summary>
    public static async ValueTask<IResult> PostLogoutAsync(
        HttpContext httpContext,
        [FromForm(Name = "ReturnUrl")] string? returnUrl
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var mediator = openIdContext.Mediator;

        // No id_token_hint is supplied: this is a first-party sign-out, so the pipeline resolves the session from the
        // OP login cookie. The end_session_endpoint remains the entry point for RP-initiated logout.
        var request = await mediator.SendAsync<LoadEndSessionRequestCommand, IEndSessionRequest>(
            new LoadEndSessionRequestCommand(openIdContext),
            httpContext.RequestAborted
        );

        await mediator.SendAsync<EndSessionCommand, EndSessionDisposition>(
            new EndSessionCommand(openIdContext, request),
            httpContext.RequestAborted
        );

        var safeReturnUrl = ResolveSafeReturnUrl(
            httpContext,
            string.IsNullOrEmpty(returnUrl)
                ? InteractiveAuthenticationDefaults.LoginPath
                : returnUrl
        );
        return TypedResults.Redirect(safeReturnUrl);
    }

    /// <summary>
    /// Serves the authorized-applications page (the single-page bundle), establishing the antiforgery token the revoke
    /// form echoes back. An anonymous request is redirected to the login page with the applications page as its return
    /// URL.
    /// </summary>
    public async ValueTask<IResult> GetApplicationsPageAsync(HttpContext httpContext)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(
            InteractiveAuthenticationDefaults.CookieSchemeName
        );
        if (!authenticateResult.Succeeded)
        {
            return TypedResults.Redirect(
                $"{InteractiveAuthenticationDefaults.LoginPath}?ReturnUrl="
                    + Uri.EscapeDataString(InteractiveAuthenticationDefaults.ApplicationsPath)
            );
        }

        return await ServeBundleAsync(httpContext);
    }

    /// <summary>
    /// Returns the current end-user's authorized applications (each with its granted scopes) as JSON for the
    /// authorized-applications page, or <c>401</c> when the request is not authenticated.
    /// </summary>
    public async ValueTask<IResult> GetApplicationsAsync(HttpContext httpContext)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(
            InteractiveAuthenticationDefaults.CookieSchemeName
        );
        if (!authenticateResult.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var subjectId = openIdContext.GetSubjectId(authenticateResult.Principal);
        if (string.IsNullOrEmpty(subjectId))
        {
            return TypedResults.Unauthorized();
        }

        var applications = await ConsentService.GetAuthorizedApplicationsAsync(
            openIdContext,
            subjectId,
            httpContext.RequestAborted
        );

        return TypedResults.Json(
            new AuthorizedApplicationsResponse(
                applications
                    .Select(app => new AuthorizedApplicationResponse(
                        app.ClientId,
                        app.ClientName,
                        app.Scopes.Select(scope => new ConsentScopeResponse(
                                scope.Value,
                                scope.Description,
                                scope.IsGranted
                            ))
                            .ToList()
                    ))
                    .ToList()
            )
        );
    }

    /// <summary>
    /// Revokes the current end-user's standing consent for a single application, then redirects back to the
    /// authorized-applications page.
    /// </summary>
    public async ValueTask<IResult> PostRevokeApplicationAsync(
        HttpContext httpContext,
        [FromForm(Name = "client_id")] string? clientId
    )
    {
        var authenticateResult = await httpContext.AuthenticateAsync(
            InteractiveAuthenticationDefaults.CookieSchemeName
        );
        if (!authenticateResult.Succeeded)
        {
            return TypedResults.Redirect(
                $"{InteractiveAuthenticationDefaults.LoginPath}?ReturnUrl="
                    + Uri.EscapeDataString(InteractiveAuthenticationDefaults.ApplicationsPath)
            );
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var subjectId = openIdContext.GetSubjectId(authenticateResult.Principal);
        if (!string.IsNullOrEmpty(subjectId) && !string.IsNullOrEmpty(clientId))
        {
            await ConsentService.RevokeConsentAsync(
                openIdContext,
                clientId,
                subjectId,
                httpContext.RequestAborted
            );
        }

        return TypedResults.Redirect(InteractiveAuthenticationDefaults.ApplicationsPath);
    }

    private async ValueTask<ContentHttpResult> ServeBundleAsync(HttpContext httpContext)
    {
        IssueAntiforgeryCookie(httpContext);

        var bundleRootPath = Options.BundleRootPath;
        if (string.IsNullOrEmpty(bundleRootPath))
        {
            return BundleMissingResult();
        }

        var indexPath = Path.Combine(bundleRootPath, "index.html");
        if (!File.Exists(indexPath))
        {
            return BundleMissingResult();
        }

        var html = await File.ReadAllTextAsync(indexPath, httpContext.RequestAborted);
        return TypedResults.Content(html, "text/html");
    }

    private void IssueAntiforgeryCookie(HttpContext httpContext)
    {
        var tokens = Antiforgery.GetAndStoreTokens(httpContext);
        if (tokens.RequestToken is not { Length: > 0 } requestToken)
        {
            return;
        }

        // A JavaScript-readable cookie (not the HttpOnly antiforgery cookie) so the single-page form can echo the
        // request token back as a hidden field, which the antiforgery middleware then validates on POST.
        httpContext.Response.Cookies.Append(
            InteractiveAuthenticationDefaults.AntiforgeryTokenCookieName,
            requestToken,
            new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Strict,
                Secure = httpContext.Request.IsHttps,
            }
        );
    }

    private static ContentHttpResult BundleMissingResult() =>
        TypedResults.Content(
            "The interactive login bundle is not available. Build the 'apps/auth' surface and configure "
                + "'InteractiveAuthentication:BundleRootPath' to its 'dist' directory.",
            "text/plain",
            contentEncoding: null,
            statusCode: StatusCodes.Status503ServiceUnavailable
        );

    private static string BuildLoginRedirect(string returnUrl, string error) =>
        $"{InteractiveAuthenticationDefaults.LoginPath}?ReturnUrl={Uri.EscapeDataString(returnUrl)}"
        + $"&error={Uri.EscapeDataString(error)}";

    // Guards against an open redirect: a local path (single leading slash, not a protocol-relative or
    // backslash-escaped authority), or an absolute URL that targets this same origin (the OP's own continue
    // endpoint), is honored; anything else falls back to the application root.
    internal static string ResolveSafeReturnUrl(HttpContext httpContext, string? returnUrl)
    {
        var request = httpContext.Request;
        var requestOrigin = $"{request.Scheme}://{request.Host.Value}";
        return ResolveSafeReturnUrl(returnUrl, requestOrigin);
    }

    internal static string ResolveSafeReturnUrl(string? returnUrl, string requestOrigin)
    {
        if (string.IsNullOrEmpty(returnUrl))
        {
            return "/";
        }

        var isLocalPath =
            returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
        if (isLocalPath)
        {
            return returnUrl;
        }

        if (
            Uri.TryCreate(returnUrl, UriKind.Absolute, out var target)
            && Uri.TryCreate(requestOrigin, UriKind.Absolute, out var origin)
            && string.Equals(target.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(target.Authority, origin.Authority, StringComparison.OrdinalIgnoreCase)
        )
        {
            return returnUrl;
        }

        return "/";
    }
}
