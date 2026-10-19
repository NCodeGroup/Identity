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

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Configures the <strong>development-only</strong> cross-origin (CORS) policy that lets a local single-page
/// application dev server call the OpenID server. Bound from the <c>Cors</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class DeveloperCorsOptions
{
    /// <summary>
    /// The configuration section (<c>Cors</c>) bound to <see cref="DeveloperCorsOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "Cors";

    /// <summary>
    /// Gets or sets the browser origins allowed to call the server cross-origin, each written exactly as a browser
    /// sends it in the <c>Origin</c> header: an <c>http</c> or <c>https</c> scheme and a host, with an optional port
    /// and no path, query, or trailing slash (for example <c>https://app.example.com</c>). An entry in any other form
    /// can never match a request, so it stops the host when it starts. When empty, a Development host allows the local
    /// Vite dev/preview origins and any other environment allows none.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
