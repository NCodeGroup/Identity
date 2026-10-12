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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Handles the interactive login/consent surface's endpoints: it serves the single-page bundle for the login and
/// consent pages, verifies credentials through the registered <see cref="IInteractiveCredentialVerifier"/>, issues the
/// cookie session on success, and clears it on logout. The login and consent forms submit as native form POSTs
/// carrying the antiforgery token, so the server-driven cookie challenge/redirect flow is preserved.
/// </summary>
internal sealed class InteractiveAuthenticationEndpointHandler(
    IInteractiveCredentialVerifier credentialVerifier,
    ISessionService sessionService,
    TimeProvider timeProvider,
    IAntiforgery antiforgery,
    InteractiveAuthenticationOptions options
)
{
    private IInteractiveCredentialVerifier CredentialVerifier { get; } = credentialVerifier;
    private ISessionService SessionService { get; } = sessionService;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IAntiforgery Antiforgery { get; } = antiforgery;
    private InteractiveAuthenticationOptions Options { get; } = options;

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
    /// Records the consent outcome: an allow returns to the original authorize URL; a deny abandons the flow. Consent
    /// is not yet durably persisted, so this is a stopgap front-end over the eventual persisted-consent decision.
    /// </summary>
    public static ValueTask<IResult> PostConsentAsync(
        HttpContext httpContext,
        [FromForm] string? action,
        [FromForm(Name = "ReturnUrl")] string? returnUrl
    )
    {
        var safeReturnUrl = ResolveSafeReturnUrl(httpContext, returnUrl);
        IResult result = string.Equals(action, "deny", StringComparison.OrdinalIgnoreCase)
            ? TypedResults.Redirect("/")
            : TypedResults.Redirect(safeReturnUrl);
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Clears the interactive cookie session and redirects to the validated local return URL.
    /// </summary>
    public static async ValueTask<IResult> LogoutAsync(
        HttpContext httpContext,
        [FromQuery(Name = "ReturnUrl")] string? returnUrl
    )
    {
        await httpContext.SignOutAsync(InteractiveAuthenticationDefaults.CookieSchemeName);
        return TypedResults.Redirect(ResolveSafeReturnUrl(httpContext, returnUrl));
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
