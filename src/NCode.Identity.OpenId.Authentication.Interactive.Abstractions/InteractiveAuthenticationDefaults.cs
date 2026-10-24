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
using Microsoft.AspNetCore.Identity;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Contains the stable paths and scheme name of the opt-in interactive login/consent surface, shared by the
/// registration, the request pipeline, and the endpoint mapping.
/// </summary>
[PublicAPI]
public static class InteractiveAuthenticationDefaults
{
    /// <summary>
    /// The cookie authentication scheme name. It matches <see cref="IdentityConstants.ApplicationScheme"/> so it lines
    /// up with the OpenID server's <c>authorization_authenticate_scheme</c> / <c>authorization_challenge_scheme</c>
    /// defaults, meaning a challenge from the authorize endpoint redirects into this surface with no per-client
    /// override.
    /// </summary>
    public static readonly string CookieSchemeName = IdentityConstants.ApplicationScheme;

    /// <summary>
    /// The base request path the interactive surface (its assets and endpoints) is served under.
    /// </summary>
    public const string BasePath = "/account";

    /// <summary>
    /// The request path of the account/session ("me") self-service page, served at the surface root. It shows the
    /// current end-user's session and hosts the first-party sign-out control.
    /// </summary>
    public const string AccountPath = "/account";

    /// <summary>
    /// The request path of the JSON endpoint that returns the current end-user's session for the account page.
    /// </summary>
    public const string MePath = "/account/me";

    /// <summary>
    /// The request path of the login page the cookie scheme's <c>LoginPath</c> redirects a challenge to.
    /// </summary>
    public const string LoginPath = "/account/login";

    /// <summary>
    /// The request path of the consent page.
    /// </summary>
    public const string ConsentPath = "/account/consent";

    /// <summary>
    /// The request path of the JSON endpoint that returns the pending authorization request (the client and the
    /// requested scopes) the consent page must present.
    /// </summary>
    public const string ConsentDetailsPath = "/account/consent/details";

    /// <summary>
    /// The request path that clears the interactive session.
    /// </summary>
    public const string LogoutPath = "/account/logout";

    /// <summary>
    /// The request path of the self-service "authorized applications" page, where the end-user reviews and revokes the
    /// applications they have granted standing consent to.
    /// </summary>
    public const string ApplicationsPath = "/account/applications";

    /// <summary>
    /// The request path of the JSON endpoint that returns the end-user's authorized applications (each with its
    /// granted scopes) for the authorized-applications page.
    /// </summary>
    public const string ApplicationsListPath = "/account/applications/list";

    /// <summary>
    /// The request path the authorized-applications page posts to in order to revoke a single application's consent.
    /// </summary>
    public const string ApplicationsRevokePath = "/account/applications/revoke";

    /// <summary>
    /// The request path of the self-service "active sessions" page, where the end-user reviews their active sessions
    /// and signs out of individual sessions, every other session, or everywhere.
    /// </summary>
    public const string SessionsPath = "/account/sessions";

    /// <summary>
    /// The request path of the JSON endpoint that returns the end-user's active sessions (each with the device/origin
    /// captured at sign-in and its lifecycle timestamps) for the active-sessions page.
    /// </summary>
    public const string SessionsListPath = "/account/sessions/list";

    /// <summary>
    /// The request path the active-sessions page posts to in order to revoke a single session by its <c>sid</c>.
    /// </summary>
    public const string SessionsRevokePath = "/account/sessions/revoke";

    /// <summary>
    /// The request path the active-sessions page posts to in order to revoke every session (optionally retaining the
    /// current one — "sign out everywhere else").
    /// </summary>
    public const string SessionsRevokeAllPath = "/account/sessions/revoke-all";

    /// <summary>
    /// The request path of the self-service change-password page, which its form posts to.
    /// </summary>
    public const string PasswordPath = "/account/password";

    /// <summary>
    /// The name of the JavaScript-readable cookie the login/consent pages echo back as the antiforgery request token,
    /// so the single-page surface can submit an antiforgery-protected form without the server rendering the token.
    /// </summary>
    public const string AntiforgeryTokenCookieName = "XSRF-TOKEN";
}
