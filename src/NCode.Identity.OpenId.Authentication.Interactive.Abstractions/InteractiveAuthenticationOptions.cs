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

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Configures the opt-in interactive login/consent surface served by the OpenID Provider host.
/// </summary>
[PublicAPI]
public sealed class InteractiveAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the file-system path to the built single-page login/consent bundle (the <c>apps/auth</c> surface),
    /// absolute or relative to the host content root. The login and consent pages serve this bundle's
    /// <c>index.html</c> and its hashed assets. When unset, the pages return guidance to build the bundle; the
    /// credential endpoints still function.
    /// </summary>
    public string? BundleRootPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the seeded surface auto-approves consent (no consent screen) for a
    /// first authorization. The consent page remains reachable for negative-path testing regardless. Defaults to
    /// <see langword="true"/> while durable consent persistence is a separate concern.
    /// </summary>
    public bool AutoApproveConsent { get; set; } = true;

    /// <summary>
    /// Gets or sets the lifetime applied to the server-side end-user session established at sign-in. When
    /// <see langword="null"/>, the session does not expire on its own and remains valid until it is revoked (for
    /// example, by logout). Defaults to <see langword="null"/>.
    /// </summary>
    public TimeSpan? SessionLifetime { get; set; }
}
