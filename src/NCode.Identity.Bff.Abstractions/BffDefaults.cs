// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The known default names and values of the <c>Backend for Frontend (BFF)</c>.
/// </summary>
/// <remarks>
/// The values are an open vocabulary: a host may configure other names, so a value that is not listed here is not
/// invalid.
/// </remarks>
[PublicAPI]
public static class BffDefaults
{
    /// <summary>
    /// The name of the authentication scheme of the BFF.
    /// </summary>
    public const string AuthenticationScheme = "Bff";

    /// <summary>
    /// The default path prefix of the endpoints of the BFF.
    /// </summary>
    public const string EndpointPrefix = "/bff";

    /// <summary>
    /// The base name of the cookie that holds the opaque session handle. The cookie of a frontend appends <c>.</c> and
    /// the name of the frontend.
    /// </summary>
    public const string SessionCookieName = "__Host-Http-bff";

    /// <summary>
    /// The base name of the cookie that binds a login transaction to the browser that started it. The cookie of a
    /// frontend appends <c>.</c> and the name of the frontend.
    /// </summary>
    public const string LoginCookieName = "__Host-Http-bff-login";

    /// <summary>
    /// The name of the cookie that holds the antiforgery token of the forms a page renders.
    /// </summary>
    public const string AntiforgeryCookieName = "__Host-Http-bff-antiforgery";

    /// <summary>
    /// The default name of the static anti-forgery request header.
    /// </summary>
    public const string AntiforgeryHeaderName = "X-CSRF";

    /// <summary>
    /// The default value of the static anti-forgery request header.
    /// </summary>
    public const string AntiforgeryHeaderValue = "1";

    /// <summary>
    /// The name of the configuration section that binds the options of the BFF.
    /// </summary>
    public const string ConfigurationSectionName = "Bff";
}
