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
    /// The name of the cookie that holds the opaque session handle.
    /// </summary>
    public const string SessionCookieName = "__Host-Http-bff";

    /// <summary>
    /// The name of the cookie that binds a login transaction to the browser that started it.
    /// </summary>
    public const string LoginCookieName = "__Host-Http-bff-login";

    /// <summary>
    /// The default name of the static anti-forgery request header.
    /// </summary>
    public const string AntiforgeryHeaderName = "X-CSRF";

    /// <summary>
    /// The default value of the static anti-forgery request header.
    /// </summary>
    public const string AntiforgeryHeaderValue = "1";

    /// <summary>
    /// The name that every session records as its frontend.
    /// </summary>
    public const string FrontendName = "default";

    /// <summary>
    /// The name of the configuration section that binds the options of the BFF.
    /// </summary>
    public const string ConfigurationSectionName = "Bff";
}
