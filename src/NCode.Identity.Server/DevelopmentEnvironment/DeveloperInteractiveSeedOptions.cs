// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Configures the <strong>development-only</strong> seeding of public single-page-application clients, confidential
/// BFF clients and a deterministic administrator user, so a local host can exercise the interactive authorization-code
/// flow and every frontend surface (the Management console, the Playground flow explorer, …) end to end. Each part is
/// inert unless its inputs are configured.
/// </summary>
[PublicAPI]
public sealed class DeveloperInteractiveSeedOptions
{
    /// <summary>
    /// The default configuration section name bound to these options.
    /// </summary>
    public const string DefaultSectionName = "DeveloperInteractiveSeed";

    /// <summary>
    /// Gets or sets the identifier of the one tenant to seed into. It is required whenever there is something to seed:
    /// seeding every tenant would hand each tenant created at runtime a well-known administrator.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the public (PKCE-only) single-page-application clients to seed, one per interactive frontend
    /// surface that runs as its own dev-server origin; read-only surfaces (for example, the conformance dashboard)
    /// perform no interactive login and so need no client here. Defaults to empty, so no client is seeded unless
    /// configuration declares one.
    /// </summary>
    /// <remarks>
    /// There are no built-in clients: a surface's client id is published by the host's <c>Frontend</c> section, so
    /// the host declares the matching client next to it (the reference host does so in its Development overlay).
    /// Starting empty also lets configuration replace the list, because the configuration binder appends the
    /// configured entries to a list that already holds some.
    /// </remarks>
    // See ADR-1370.
    public IList<DeveloperSpaClientSeed> SpaClients { get; set; } = [];

    /// <summary>
    /// Gets or sets the confidential BFF clients to seed, one per frontend surface served by a BFF host of its own.
    /// Defaults to empty, so no client is seeded unless configuration declares one.
    /// </summary>
    /// <remarks>
    /// There are no built-in clients. Starting empty also lets configuration replace the list, because the
    /// configuration binder appends the configured entries to a list that already holds some.
    /// </remarks>
    public IList<DeveloperBffClientSeed> BffClients { get; set; } = [];

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
    /// Gets a value indicating whether an administrator user should be seeded.
    /// </summary>
    public bool SeedAdminUser =>
        !string.IsNullOrEmpty(AdminUserName) && !string.IsNullOrEmpty(AdminPassword);
}

/// <summary>
/// Describes a single <strong>development-only</strong> public (PKCE-only) single-page-application client to seed: its
/// client id and the exact-match redirect URIs its dev-server origin registers.
/// </summary>
[PublicAPI]
public sealed class DeveloperSpaClientSeed
{
    /// <summary>
    /// Gets or sets the public (PKCE-only) single-page-application client id. When empty, this entry is skipped.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the redirect URIs registered for this client.
    /// </summary>
    public string[] RedirectUris { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the authorization endpoint must obtain (and persist) end-user consent
    /// for this client's requested scopes. Defaults to <see langword="false"/>.
    /// </summary>
    public bool RequireConsent { get; set; }

    /// <summary>
    /// Gets a value indicating whether this client should be seeded.
    /// </summary>
    public bool ShouldSeed => !string.IsNullOrEmpty(ClientId) && RedirectUris.Length > 0;
}
