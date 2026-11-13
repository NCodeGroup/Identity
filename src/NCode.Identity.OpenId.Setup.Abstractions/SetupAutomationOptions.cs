// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Describes the automation client that the headless <c>setup</c> command registers: a platform-owned
/// <c>client_credentials</c> machine client that holds <c>GlobalAdmin</c>, so an unattended install manages the
/// instance without the development bootstrap client. Bound from the <c>Setup:Automation</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class SetupAutomationOptions
{
    /// <summary>
    /// Gets or sets the requested, stable <c>client_id</c> of the automation client.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the JSON text of the automation client's public JWK Set, which setup registers by value.
    /// </summary>
    public string? Jwks { get; set; }

    /// <summary>
    /// Gets a value indicating whether the automation client is configured, which is when <see cref="ClientId"/> or
    /// <see cref="Jwks"/> is set.
    /// </summary>
    public bool IsConfigured => ClientId is not null || Jwks is not null;
}
