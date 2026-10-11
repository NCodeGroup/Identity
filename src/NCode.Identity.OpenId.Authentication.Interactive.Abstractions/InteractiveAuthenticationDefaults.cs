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
    /// The request path of the login page the cookie scheme's <c>LoginPath</c> redirects a challenge to.
    /// </summary>
    public const string LoginPath = "/account/login";

    /// <summary>
    /// The request path of the consent page.
    /// </summary>
    public const string ConsentPath = "/account/consent";

    /// <summary>
    /// The request path that clears the interactive session.
    /// </summary>
    public const string LogoutPath = "/account/logout";

    /// <summary>
    /// The name of the JavaScript-readable cookie the login/consent pages echo back as the antiforgery request token,
    /// so the single-page surface can submit an antiforgery-protected form without the server rendering the token.
    /// </summary>
    public const string AntiforgeryTokenCookieName = "XSRF-TOKEN";
}
