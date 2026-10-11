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
/// Configures the <strong>development-only</strong> seeding of a public single-page-application client and a
/// deterministic administrator user, so a local host can exercise the interactive authorization-code + PKCE flow and
/// the management console end to end. Each half is inert unless its inputs are configured.
/// </summary>
[PublicAPI]
public sealed class DeveloperInteractiveSeedOptions
{
    /// <summary>
    /// The default configuration section name bound to these options.
    /// </summary>
    public const string DefaultSectionName = "DeveloperInteractiveSeed";

    /// <summary>
    /// Gets or sets the workload tenant id to seed into. When empty, every workload tenant is seeded.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the public (PKCE-only) single-page-application client id to seed. When empty, no client is seeded.
    /// </summary>
    public string? SpaClientId { get; set; } = "mgmt-spa";

    /// <summary>
    /// Gets or sets the redirect URIs registered for the single-page-application client.
    /// </summary>
    public string[] SpaRedirectUris { get; set; } =
    ["http://localhost:5173/callback", "https://localhost:5173/callback"];

    /// <summary>
    /// Gets or sets the administrator user name to seed. When empty, no user is seeded.
    /// </summary>
    public string? AdminUserName { get; set; } = "admin";

    /// <summary>
    /// Gets or sets the administrator email address.
    /// </summary>
    public string? AdminEmail { get; set; } = "admin@localhost";

    /// <summary>
    /// Gets or sets the administrator password. When empty, no user is seeded (there is intentionally no default
    /// password).
    /// </summary>
    public string? AdminPassword { get; set; }

    /// <summary>
    /// Gets a value indicating whether a single-page-application client should be seeded.
    /// </summary>
    public bool SeedSpaClient => !string.IsNullOrEmpty(SpaClientId) && SpaRedirectUris.Length > 0;

    /// <summary>
    /// Gets a value indicating whether an administrator user should be seeded.
    /// </summary>
    public bool SeedAdminUser =>
        !string.IsNullOrEmpty(AdminUserName) && !string.IsNullOrEmpty(AdminPassword);
}
