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
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Consent;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Settings;
using NCode.Json;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The <see cref="IEndpointProvider"/> for the interactive login/consent/account surface. It serves the single-page
/// bundle for the login, consent, and account ("me") pages, verifies credentials through the registered
/// <see cref="IInteractiveCredentialVerifier"/>, issues the cookie session on success, exposes the current session as
/// JSON, and clears it on logout. The login, consent, and logout forms submit as native form POSTs carrying the
/// antiforgery token, so the server-driven cookie challenge/redirect flow is preserved. The self-service surface
/// serves the cookie only while the session it names is active.
/// </summary>
internal sealed class InteractiveAuthenticationEndpointHandler(
    IInteractiveCredentialVerifier credentialVerifier,
    IPrincipalResolver principalResolver,
    IConsentService consentService,
    IContinueRefusalService continueRefusalService,
    IInteractivePasswordChanger passwordChanger,
    ISessionService sessionService,
    TimeProvider timeProvider,
    IAntiforgery antiforgery,
    InteractiveBundleReader bundleReader,
    InteractiveSurfacePaths surfacePaths
) : IEndpointProvider
{
    // The `scope` form value that selects "sign out everywhere else" (retain the current session); any other value is
    // a full "sign out everywhere".
    private const string SignOutOthersScope = "others";

    // The `error` and `status` query values the change-password page reads back to show its message.
    private const string ErrorQueryName = "error";
    private const string StatusQueryName = "status";
    private const string PasswordChangedStatus = "changed";

    private IInteractiveCredentialVerifier CredentialVerifier { get; } = credentialVerifier;
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;
    private IConsentService ConsentService { get; } = consentService;
    private IContinueRefusalService ContinueRefusalService { get; } = continueRefusalService;
    private IInteractivePasswordChanger PasswordChanger { get; } = passwordChanger;
    private ISessionService SessionService { get; } = sessionService;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IAntiforgery Antiforgery { get; } = antiforgery;
    private InteractiveBundleReader BundleReader { get; } = bundleReader;
    private InteractiveSurfacePaths SurfacePaths { get; } = surfacePaths;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        // The user agent navigates to the surface, and its own pages call it from the same origin; no CORS policy
        // applies to it (FAPI 2.0 Security Profile §5.3.2.1).
        var surface = endpoints.MapGroup(string.Empty).WithOpenIdFrontChannel();

        surface.MapGet(InteractiveAuthenticationDefaults.AccountPath, GetAccountPageAsync);
        surface.MapGet(InteractiveAuthenticationDefaults.MePath, GetMeAsync);
        surface.MapGet(InteractiveAuthenticationDefaults.LoginPath, GetLoginPageAsync);
        surface.MapPost(InteractiveAuthenticationDefaults.LoginPath, PostLoginAsync);
        surface.MapGet(InteractiveAuthenticationDefaults.ConsentPath, GetConsentPageAsync);
        surface.MapGet(
            InteractiveAuthenticationDefaults.ConsentDetailsPath,
            GetConsentDetailsAsync
        );
        surface.MapPost(InteractiveAuthenticationDefaults.ConsentPath, PostConsentAsync);
        surface.MapPost(InteractiveAuthenticationDefaults.LogoutPath, PostLogoutAsync);
        surface.MapGet(
            InteractiveAuthenticationDefaults.ApplicationsPath,
            GetApplicationsPageAsync
        );
        surface.MapGet(
            InteractiveAuthenticationDefaults.ApplicationsListPath,
            GetApplicationsAsync
        );
        surface.MapPost(
            InteractiveAuthenticationDefaults.ApplicationsRevokePath,
            PostRevokeApplicationAsync
        );
        surface.MapGet(InteractiveAuthenticationDefaults.SessionsPath, GetSessionsPageAsync);
        surface.MapGet(InteractiveAuthenticationDefaults.SessionsListPath, GetSessionsAsync);
        surface.MapPost(
            InteractiveAuthenticationDefaults.SessionsRevokePath,
            PostRevokeSessionAsync
        );
        surface.MapPost(
            InteractiveAuthenticationDefaults.SessionsRevokeAllPath,
            PostRevokeAllSessionsAsync
        );
        surface.MapGet(InteractiveAuthenticationDefaults.PasswordPath, GetPasswordPageAsync);
        surface.MapPost(InteractiveAuthenticationDefaults.PasswordPath, PostPasswordAsync);
    }

    /// <summary>
    /// Serves the login page. The server renders a working no-JavaScript login form (progressive-enhancement baseline)
    /// carrying the antiforgery token and the return URL; when the single-page bundle is present the form is injected
    /// into its <c>#root</c> so the SPA replaces it on mount.
    /// </summary>
    public async ValueTask<IResult> GetLoginPageAsync(HttpContext httpContext)
    {
        var tokens = IssueAntiforgeryTokens(httpContext);
        var query = httpContext.Request.Query;
        var html = InteractiveHtmlResult.CreateBuffer();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            html,
            await BundleReader.TryReadShellAsync(httpContext.RequestAborted),
            SurfacePaths.GetSurfaceBase(httpContext),
            ReadBranding(httpContext),
            query["ReturnUrl"].ToString(),
            tokens.FormFieldName,
            tokens.RequestToken ?? string.Empty,
            DescribeLoginError(query["error"].ToString())
        );
        return new InteractiveHtmlResult(html);
    }

    /// <summary>
    /// Serves the consent page. The server resolves the pending authorization request and renders a working
    /// no-JavaScript consent form (a per-scope checkbox plus allow/deny) carrying the antiforgery token; when the
    /// single-page bundle is present the form is injected into its <c>#root</c> so the SPA replaces it on mount.
    /// </summary>
    public async ValueTask<IResult> GetConsentPageAsync(HttpContext httpContext)
    {
        var tokens = IssueAntiforgeryTokens(httpContext);
        var rawReturnUrl = httpContext.Request.Query["ReturnUrl"].ToString();

        var appName = "An application";
        IReadOnlyList<InteractiveAuthenticationPageRenderer.ScopeView> scopes = [];

        var authenticateResult = await AuthenticateSessionAsync(httpContext);
        if (authenticateResult.Succeeded && !string.IsNullOrEmpty(rawReturnUrl))
        {
            var openIdContext = httpContext.GetOpenIdContext();
            var subjectId = openIdContext.GetSubjectId(authenticateResult.Principal);
            if (!string.IsNullOrEmpty(subjectId))
            {
                var pending = await ConsentService.ResolvePendingConsentAsync(
                    openIdContext,
                    subjectId,
                    rawReturnUrl,
                    httpContext.RequestAborted
                );
                if (pending is { } value)
                {
                    appName = string.IsNullOrEmpty(value.ClientName)
                        ? value.ClientId
                        : value.ClientName;
                    scopes = value
                        .Scopes.Select(scope => new InteractiveAuthenticationPageRenderer.ScopeView(
                            scope.Value,
                            scope.Description,
                            // A consent screen pre-selects every requested scope so Allow authorizes the request;
                            // the subject unchecks a scope to down-scope. Defaulting to the prior-granted state would
                            // silently drop a freshly-requested scope (e.g. openid) when the subject clicks Allow.
                            Checked: true
                        ))
                        .ToList();
                }
            }
        }

        var html = InteractiveHtmlResult.CreateBuffer();
        InteractiveAuthenticationPageRenderer.RenderConsentPage(
            html,
            await BundleReader.TryReadShellAsync(httpContext.RequestAborted),
            SurfacePaths.GetSurfaceBase(httpContext),
            ReadBranding(httpContext),
            rawReturnUrl,
            tokens.FormFieldName,
            tokens.RequestToken ?? string.Empty,
            appName,
            scopes
        );
        return new InteractiveHtmlResult(html);
    }

    /// <summary>
    /// Serves the account ("me") page (the single-page bundle), establishing the antiforgery token the sign-out form
    /// echoes back. An anonymous request is redirected to the login page with the account page as its return URL.
    /// </summary>
    public async ValueTask<IResult> GetAccountPageAsync(HttpContext httpContext)
    {
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
        if (!authenticateResult.Succeeded)
        {
            return RedirectToLogin(httpContext, InteractiveAuthenticationDefaults.AccountPath);
        }

        return await ServeBundleAsync(httpContext);
    }

    /// <summary>
    /// Returns the current end-user's session as JSON for the account page, or <c>401</c> when the request is not
    /// authenticated.
    /// </summary>
    public async ValueTask<IResult> GetMeAsync(HttpContext httpContext)
    {
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
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

        var authTimeValue = principal.FindFirst(JoseClaimNames.Payload.AuthTime)?.Value;
        long? authTime = DateCoercion.TryParseNumericDate(authTimeValue, out var parsedAuthTime)
            ? parsedAuthTime.ToUnixTimeSeconds()
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
    /// An end user whose standing is refused, such as a blocked principal or a disabled account, signs in nowhere: no
    /// session is created, and the parked authorization request the return URL names is answered with the refusal,
    /// otherwise the login page shows it.
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
            return TypedResults.Redirect(
                BuildLoginRedirect(httpContext, safeReturnUrl, "invalid_request")
            );
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
            return TypedResults.Redirect(
                BuildLoginRedirect(httpContext, safeReturnUrl, "invalid_credentials")
            );
        }

        // Resolve the verified credential to the stable, server-owned principal id and normalize the cookie subject to
        // it, so the session, every subsequent authorize request, and the grants they issue all key on the single
        // principal regardless of how the user authenticates (ADR-0035). Without this the interactive path would carry
        // the raw local-account id while ROPC/bearer carry the resolved principal, keying the same user's grants
        // inconsistently. The same tenant-aware normalization seam enforces the Subject.sub == PrincipalId invariant at
        // every ticket-construction site (ADR-0094).
        principal = await NormalizeSubjectToPrincipalAsync(
            openIdContext,
            principal,
            httpContext.RequestAborted
        );

        // The end user's standing is validated before a session is created, so a blocked principal or a disabled
        // account signs in nowhere.
        // See ADR-0229.
        var standing = await ValidateStandingAsync(
            openIdContext,
            principal,
            httpContext.RequestAborted
        );
        if (standing.HasError)
        {
            return await RefuseSignInAsync(
                httpContext,
                openIdContext,
                safeReturnUrl,
                standing.Error
            );
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

    private async ValueTask<IResult> RefuseSignInAsync(
        HttpContext httpContext,
        OpenIdContext openIdContext,
        string safeReturnUrl,
        IOpenIdError error
    )
    {
        var refusal = await ContinueRefusalService.RefuseAsync(
            openIdContext,
            safeReturnUrl,
            error,
            httpContext.RequestAborted
        );
        if (refusal.HasHttpResult)
        {
            return refusal.HttpResult;
        }

        return TypedResults.Redirect(
            BuildLoginRedirect(httpContext, safeReturnUrl, OpenIdConstants.ErrorCodes.AccessDenied)
        );
    }

    private static async ValueTask<OperationDisposition<IOpenIdError>> ValidateStandingAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;
        var operationDisposition = new OperationDisposition<IOpenIdError>();

        await mediator.SendAsync(
            new ValidateSubjectStandingCommand(
                openIdContext,
                new SubjectAuthentication(
                    InteractiveAuthenticationDefaults.CookieSchemeName,
                    new AuthenticationProperties(),
                    principal,
                    openIdContext.GetSubjectId(principal) ?? string.Empty
                ),
                operationDisposition
            ),
            cancellationToken
        );

        return operationDisposition;
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

        // Session lifecycle policy is tenant-scoped data (a session spans every client the subject signs into), read
        // from the tenant's effective settings. Both session bounds always apply; a zero max_concurrent_sessions
        // disables the cap.
        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        var absoluteLifetime = settings.GetValue(OpenIdSettingKeys.SessionAbsoluteLifetime);
        var idleTimeout = settings.GetValue(OpenIdSettingKeys.SessionIdleTimeout);
        var maxConcurrentSessions = settings.GetValue(OpenIdSettingKeys.MaxConcurrentSessions);

        // Capture the device/origin at sign-in so the self-service active-sessions surface can describe each session.
        var httpRequest = openIdContext.Http.Request;
        var userAgent = httpRequest.Headers.UserAgent.ToString();
        var remoteIpAddress = openIdContext.Http.Connection.RemoteIpAddress?.ToString();

        var request = new CreateSessionRequest
        {
            SubjectId = subjectId,
            AuthTime = ReadAuthTime(principal),
            AuthenticationMethods = amr.Count > 0 ? amr : null,
            AuthenticationContextClassReference = principal
                .FindFirst(JoseClaimNames.Payload.Acr)
                ?.Value,
            IdentityProvider = principal.FindFirst(JoseClaimNames.Payload.Idp)?.Value,
            AbsoluteLifetime = absoluteLifetime,
            IdleTimeout = idleTimeout,
            MaxConcurrentSessions = maxConcurrentSessions > 0 ? maxConcurrentSessions : null,
            UserAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent,
            RemoteIpAddress = remoteIpAddress,
        };

        var sessionId = await SessionService.CreateAsync(openIdContext, request, cancellationToken);
        identity.AddClaim(new Claim(JoseClaimNames.Payload.Sid, sessionId));
    }

    /// <summary>
    /// Resolves the verified credential to the stable, server-owned principal id and returns a principal whose subject
    /// claim is that principal id, so the cookie, session, and every grant key on one subject (ADR-0035).
    /// </summary>
    private async ValueTask<ClaimsPrincipal> NormalizeSubjectToPrincipalAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken
    )
    {
        var principalId = await PrincipalResolver.ResolvePrincipalIdAsync(
            openIdContext,
            principal,
            cancellationToken
        );
        if (string.IsNullOrEmpty(principalId))
        {
            return principal;
        }

        return openIdContext.NormalizeSubjectId(principal, principalId);
    }

    private DateTimeOffset ReadAuthTime(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(JoseClaimNames.Payload.AuthTime)?.Value;
        return DateCoercion.TryParseNumericDate(value, out var authTime)
            ? authTime
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
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
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
    /// resume; a deny answers the parked authorization request with an <c>access_denied</c> error at the client's
    /// redirect URI, or returns to the home page when the return URL names no parked request.
    /// </summary>
    public async ValueTask<IResult> PostConsentAsync(
        HttpContext httpContext,
        [FromForm] string? action,
        [FromForm(Name = "ReturnUrl")] string? returnUrl
    )
    {
        var safeReturnUrl = ResolveSafeReturnUrl(httpContext, returnUrl);
        var openIdContext = httpContext.GetOpenIdContext();

        if (string.Equals(action, "deny", StringComparison.OrdinalIgnoreCase))
        {
            // A denial answers the parked authorization request with `access_denied` (OpenID Connect Core 1.0
            // §3.1.2.6).
            // See ADR-0229.
            var refusal = await ContinueRefusalService.RefuseAsync(
                openIdContext,
                safeReturnUrl,
                openIdContext.ErrorFactory.AccessDenied(
                    "The end user denied the authorization request."
                ),
                httpContext.RequestAborted
            );
            return refusal.HasHttpResult ? refusal.HttpResult : TypedResults.Redirect("/");
        }

        var authenticateResult = await AuthenticateSessionAsync(httpContext);
        if (!authenticateResult.Succeeded)
        {
            return TypedResults.Redirect(
                BuildLoginRedirect(httpContext, safeReturnUrl, "invalid_request")
            );
        }

        var subjectId = openIdContext.GetSubjectId(authenticateResult.Principal);
        if (string.IsNullOrEmpty(subjectId) || string.IsNullOrEmpty(returnUrl))
        {
            return TypedResults.Redirect(
                BuildLoginRedirect(httpContext, safeReturnUrl, "invalid_request")
            );
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
    public async ValueTask<IResult> PostLogoutAsync(
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
                ? SurfacePaths.GetSurfacePath(
                    httpContext,
                    InteractiveAuthenticationDefaults.LoginPath
                )
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
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
        if (!authenticateResult.Succeeded)
        {
            return RedirectToLogin(httpContext, InteractiveAuthenticationDefaults.ApplicationsPath);
        }

        return await ServeBundleAsync(httpContext);
    }

    /// <summary>
    /// Returns the current end-user's authorized applications (each with its granted scopes) as JSON for the
    /// authorized-applications page, or <c>401</c> when the request is not authenticated.
    /// </summary>
    public async ValueTask<IResult> GetApplicationsAsync(HttpContext httpContext)
    {
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
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
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
        if (!authenticateResult.Succeeded)
        {
            return RedirectToLogin(httpContext, InteractiveAuthenticationDefaults.ApplicationsPath);
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

        return TypedResults.Redirect(
            SurfacePaths.GetSurfacePath(
                httpContext,
                InteractiveAuthenticationDefaults.ApplicationsPath
            )
        );
    }

    /// <summary>
    /// Serves the active-sessions page (the single-page bundle), establishing the antiforgery token the revoke forms
    /// echo back. An anonymous request is redirected to the login page with the sessions page as its return URL.
    /// </summary>
    public async ValueTask<IResult> GetSessionsPageAsync(HttpContext httpContext)
    {
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
        if (!authenticateResult.Succeeded)
        {
            return RedirectToLogin(httpContext, InteractiveAuthenticationDefaults.SessionsPath);
        }

        return await ServeBundleAsync(httpContext);
    }

    /// <summary>
    /// Returns the current end-user's active sessions as JSON for the active-sessions page, marking the request's own
    /// session, or <c>401</c> when the request is not authenticated.
    /// </summary>
    public async ValueTask<IResult> GetSessionsAsync(HttpContext httpContext)
    {
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
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

        var currentSessionId = principal.FindFirst(JoseClaimNames.Payload.Sid)?.Value;

        var sessions = await SessionService.GetSessionsAsync(
            openIdContext,
            subjectId,
            httpContext.RequestAborted
        );

        return TypedResults.Json(
            new SessionsResponse(
                sessions.Select(session => ToSummary(session, currentSessionId)).ToList()
            )
        );
    }

    // Projects a session into the active-sessions page's view model, marking the request's own session.
    private static SessionSummaryResponse ToSummary(SessionInfo session, string? currentSessionId)
    {
        var grant = session.Grant;
        return new SessionSummaryResponse(
            session.SessionId,
            string.Equals(session.SessionId, currentSessionId, StringComparison.Ordinal),
            session.CreatedWhen.ToUnixTimeSeconds(),
            grant.LastActivityWhen.ToUnixTimeSeconds(),
            session.ExpiresWhen.ToUnixTimeSeconds(),
            grant.AuthenticationMethods ?? [],
            grant.IdentityProvider,
            grant.UserAgent,
            grant.RemoteIpAddress
        );
    }

    /// <summary>
    /// Revokes a single one of the current end-user's sessions by its <c>sid</c> (self-service), then redirects back to
    /// the active-sessions page. The session is revoked only when it belongs to the authenticated subject, so a user
    /// cannot terminate another subject's session.
    /// </summary>
    public async ValueTask<IResult> PostRevokeSessionAsync(
        HttpContext httpContext,
        [FromForm(Name = "sid")] string? sessionId
    )
    {
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
        if (!authenticateResult.Succeeded)
        {
            return RedirectToLogin(httpContext, InteractiveAuthenticationDefaults.SessionsPath);
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var subjectId = openIdContext.GetSubjectId(authenticateResult.Principal);
        if (!string.IsNullOrEmpty(subjectId) && !string.IsNullOrEmpty(sessionId))
        {
            // Resolve the session and confirm it belongs to the authenticated subject before revoking, so a crafted
            // sid cannot terminate a session the caller does not own.
            var session = await SessionService.GetOrDefaultAsync(
                openIdContext,
                sessionId,
                httpContext.RequestAborted
            );
            if (
                session is { } found
                && string.Equals(found.SubjectId, subjectId, StringComparison.Ordinal)
            )
            {
                await SessionService.RevokeAsync(
                    openIdContext,
                    sessionId,
                    SessionTerminationReasons.UserInitiated,
                    httpContext.RequestAborted
                );

                // Revoking the request's own session invalidates the login cookie; clear it so the next request
                // re-authenticates instead of presenting a cookie whose session no longer exists.
                var currentSessionId = authenticateResult
                    .Principal.FindFirst(JoseClaimNames.Payload.Sid)
                    ?.Value;
                if (string.Equals(sessionId, currentSessionId, StringComparison.Ordinal))
                {
                    await httpContext.SignOutAsync(
                        InteractiveAuthenticationDefaults.CookieSchemeName
                    );
                }
            }
        }

        return TypedResults.Redirect(
            SurfacePaths.GetSurfacePath(httpContext, InteractiveAuthenticationDefaults.SessionsPath)
        );
    }

    /// <summary>
    /// Revokes the current end-user's sessions in bulk (self-service), then redirects back to the active-sessions page.
    /// When <c>scope</c> is <c>others</c> ("sign out everywhere else") the request's own session is retained; otherwise
    /// every session is revoked and the login cookie is cleared ("sign out everywhere").
    /// Signing out everywhere else also revokes every grant the end user holds under no session.
    /// </summary>
    public async ValueTask<IResult> PostRevokeAllSessionsAsync(
        HttpContext httpContext,
        [FromForm(Name = "scope")] string? scope
    )
    {
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
        if (!authenticateResult.Succeeded)
        {
            return RedirectToLogin(httpContext, InteractiveAuthenticationDefaults.SessionsPath);
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var principal = authenticateResult.Principal;
        var subjectId = openIdContext.GetSubjectId(principal);
        if (string.IsNullOrEmpty(subjectId))
        {
            return TypedResults.Redirect(
                SurfacePaths.GetSurfacePath(
                    httpContext,
                    InteractiveAuthenticationDefaults.SessionsPath
                )
            );
        }

        var currentSessionId = principal.FindFirst(JoseClaimNames.Payload.Sid)?.Value;

        // "Sign out everywhere else" retains the request's own session; it needs a known current sid to exclude.
        if (
            string.Equals(scope, SignOutOthersScope, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(currentSessionId)
        )
        {
            await SessionService.RevokeAllExceptAsync(
                openIdContext,
                subjectId,
                currentSessionId,
                SessionTerminationReasons.UserInitiated,
                httpContext.RequestAborted
            );

            return TypedResults.Redirect(
                SurfacePaths.GetSurfacePath(
                    httpContext,
                    InteractiveAuthenticationDefaults.SessionsPath
                )
            );
        }

        await SessionService.RevokeAllAsync(
            openIdContext,
            subjectId,
            SessionTerminationReasons.GlobalSignOut,
            httpContext.RequestAborted
        );

        // A full sign-out revokes the request's own session too, so clear the login cookie and return to the login page.
        await httpContext.SignOutAsync(InteractiveAuthenticationDefaults.CookieSchemeName);
        return TypedResults.Redirect(
            SurfacePaths.GetSurfacePath(httpContext, InteractiveAuthenticationDefaults.LoginPath)
        );
    }

    /// <summary>
    /// Serves the change-password page (the single-page bundle), establishing the antiforgery token its form echoes
    /// back. A request without an active session is redirected to the login page with the change-password page as its
    /// return URL.
    /// </summary>
    public async ValueTask<IResult> GetPasswordPageAsync(HttpContext httpContext)
    {
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
        if (!authenticateResult.Succeeded)
        {
            return RedirectToLogin(httpContext, InteractiveAuthenticationDefaults.PasswordPath);
        }

        return await ServeBundleAsync(httpContext);
    }

    /// <summary>
    /// Changes the current end-user's password from the change-password form, then redirects back to the page with the
    /// outcome: <c>?status=changed</c>, or <c>?error=</c> with <c>invalid_request</c> for a missing field,
    /// <c>password_mismatch</c> when the confirmation differs from the new password, or the error code the credential
    /// backend reports: <c>invalid_current_password</c> when the current password is wrong, <c>not_supported</c> when
    /// it has no password to change, or a password policy code such as <c>password_too_short</c>. A change keeps the
    /// current session and ends every other session. A request without an active session is redirected to the login
    /// page.
    /// </summary>
    public async ValueTask<IResult> PostPasswordAsync(
        HttpContext httpContext,
        [FromForm(Name = "current_password")] string? currentPassword,
        [FromForm(Name = "new_password")] string? newPassword,
        [FromForm(Name = "confirm_password")] string? confirmPassword
    )
    {
        var authenticateResult = await AuthenticateSessionAsync(httpContext);
        if (!authenticateResult.Succeeded)
        {
            return RedirectToLogin(httpContext, InteractiveAuthenticationDefaults.PasswordPath);
        }

        var openIdContext = httpContext.GetOpenIdContext();
        var principal = authenticateResult.Principal;
        var subjectId = openIdContext.GetSubjectId(principal);
        var currentSessionId = principal.FindFirst(JoseClaimNames.Payload.Sid)?.Value;
        if (string.IsNullOrEmpty(subjectId) || string.IsNullOrEmpty(currentSessionId))
        {
            return RedirectToLogin(httpContext, InteractiveAuthenticationDefaults.PasswordPath);
        }

        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
        {
            return RedirectToPasswordPage(
                httpContext,
                ErrorQueryName,
                InteractivePasswordChangeErrors.InvalidRequest
            );
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            return RedirectToPasswordPage(
                httpContext,
                ErrorQueryName,
                InteractivePasswordChangeErrors.PasswordMismatch
            );
        }

        var result = await PasswordChanger.ChangePasswordAsync(
            openIdContext,
            principal,
            currentPassword,
            newPassword,
            httpContext.RequestAborted
        );
        if (!result.IsChanged)
        {
            return RedirectToPasswordPage(httpContext, ErrorQueryName, result.Error);
        }

        // A change keeps the current session, ends every other one and revokes the grants issued under no session.
        // The password is already saved, so a client disconnect does not cancel the termination.
        // See ADR-0226 and ADR-0230.
        await SessionService.RevokeAllExceptAsync(
            openIdContext,
            subjectId,
            currentSessionId,
            SessionTerminationReasons.PasswordChanged,
            CancellationToken.None
        );
        return RedirectToPasswordPage(httpContext, StatusQueryName, PasswordChangedStatus);
    }

    /// <summary>
    /// Authenticates the login cookie and requires the session it names to be active, so a cookie whose session has
    /// ended or been revoked serves nothing. A cookie without an active session is cleared and reported as no result.
    /// </summary>
    // See ADR-0229.
    private async ValueTask<AuthenticateResult> AuthenticateSessionAsync(HttpContext httpContext)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(
            InteractiveAuthenticationDefaults.CookieSchemeName
        );
        if (!authenticateResult.Succeeded)
        {
            return authenticateResult;
        }

        // The cookie serves the surface only while the session it names is active, so the cookie of an ended, revoked,
        // or blocked end user's session serves nothing.
        var sessionId = authenticateResult.Principal.FindFirst(JoseClaimNames.Payload.Sid)?.Value;
        if (
            string.IsNullOrEmpty(sessionId)
            || !await SessionService.ValidateAndTouchAsync(
                httpContext.GetOpenIdContext(),
                sessionId,
                httpContext.RequestAborted
            )
        )
        {
            await httpContext.SignOutAsync(InteractiveAuthenticationDefaults.CookieSchemeName);
            return AuthenticateResult.NoResult();
        }

        return authenticateResult;
    }

    private async ValueTask<IResult> ServeBundleAsync(HttpContext httpContext)
    {
        IssueAntiforgeryTokens(httpContext);

        var shell = await BundleReader.TryReadShellAsync(httpContext.RequestAborted);
        if (shell is null)
        {
            return BundleMissingResult();
        }

        var html = InteractiveHtmlResult.CreateBuffer();
        InteractiveAuthenticationPageRenderer.RenderBundlePage(
            html,
            shell,
            SurfacePaths.GetSurfaceBase(httpContext),
            ReadBranding(httpContext)
        );
        return new InteractiveHtmlResult(html);
    }

    // The tenant the request resolved is the one whose page the bundle themes itself for (ADR-0253).
    private static InteractiveBranding ReadBranding(HttpContext httpContext) =>
        InteractiveBranding.FromSettings(
            httpContext.GetOpenIdContext().Tenant.SettingsProvider.Collection
        );

    // Mirrors the apps/auth describeLoginError mapping so the no-JS login form shows the same messages.
    private static string? DescribeLoginError(string? error) =>
        error switch
        {
            null or "" => null,
            "invalid_credentials" => "The username or password is incorrect.",
            "invalid_request" => "Enter both a username and a password.",
            OpenIdConstants.ErrorCodes.AccessDenied =>
                "This account cannot sign in. Contact your administrator.",
            _ => "Sign-in failed. Please try again.",
        };

    private AntiforgeryTokenSet IssueAntiforgeryTokens(HttpContext httpContext)
    {
        var tokens = Antiforgery.GetAndStoreTokens(httpContext);
        if (tokens.RequestToken is { Length: > 0 } requestToken)
        {
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

        return tokens;
    }

    private static ContentHttpResult BundleMissingResult() =>
        TypedResults.Content(
            "The interactive login bundle is not available. Build the 'apps/auth' surface and configure "
                + "'InteractiveAuthentication:BundleRootPath' to its 'dist' directory.",
            "text/plain",
            contentEncoding: null,
            statusCode: StatusCodes.Status503ServiceUnavailable
        );

    private string BuildLoginUrl(HttpContext httpContext, string returnUrl) =>
        $"{SurfacePaths.GetSurfacePath(httpContext, InteractiveAuthenticationDefaults.LoginPath)}?ReturnUrl={Uri.EscapeDataString(returnUrl)}";

    private RedirectHttpResult RedirectToLogin(HttpContext httpContext, string returnSurfacePath) =>
        TypedResults.Redirect(
            BuildLoginUrl(httpContext, SurfacePaths.GetSurfacePath(httpContext, returnSurfacePath))
        );

    private string BuildLoginRedirect(HttpContext httpContext, string returnUrl, string error) =>
        BuildLoginUrl(httpContext, returnUrl) + $"&error={Uri.EscapeDataString(error)}";

    private RedirectHttpResult RedirectToPasswordPage(
        HttpContext httpContext,
        string queryName,
        string queryValue
    ) =>
        TypedResults.Redirect(
            $"{SurfacePaths.GetSurfacePath(httpContext, InteractiveAuthenticationDefaults.PasswordPath)}"
                + $"?{queryName}={Uri.EscapeDataString(queryValue)}"
        );

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
