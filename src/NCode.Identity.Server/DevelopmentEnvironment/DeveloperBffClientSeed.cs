// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Describes a single <strong>development-only</strong> confidential BFF client to seed: a client that authenticates
/// with <c>private_key_jwt</c>, pushes its authorization requests, receives DPoP-bound tokens and takes back-channel
/// logout. Its public keys are registered by value, because the server's outbound guard never reaches a loopback
/// <c>jwks_uri</c>.
/// </summary>
[PublicAPI]
public sealed class DeveloperBffClientSeed
{
    /// <summary>
    /// Gets or sets the confidential BFF client id. When empty, this entry is skipped.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the JSON text of the public JSON Web Key Set registered by value for this client. It is supplied
    /// from outside the repository, such as the AppHost. When empty, this entry is skipped.
    /// </summary>
    public string? Jwks { get; set; }

    /// <summary>
    /// Gets or sets the redirect URIs registered for this client.
    /// </summary>
    public string[] RedirectUris { get; set; } = [];

    /// <summary>
    /// Gets or sets the post-logout redirect URIs registered for this client.
    /// </summary>
    public string[] PostLogoutRedirectUris { get; set; } = [];

    /// <summary>
    /// Gets or sets the back-channel logout URI registered for this client. When empty, no back-channel logout URI is
    /// registered.
    /// </summary>
    public string? BackchannelLogoutUri { get; set; }

    /// <summary>
    /// Gets a value indicating whether this client should be seeded.
    /// </summary>
    public bool ShouldSeed =>
        !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(Jwks) && RedirectUris.Length > 0;
}
