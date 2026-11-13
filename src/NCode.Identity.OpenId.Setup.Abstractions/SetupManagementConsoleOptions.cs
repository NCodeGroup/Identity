// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Describes the management console's confidential client that setup registers. Bound from the
/// <c>Setup:ManagementConsole</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class SetupManagementConsoleOptions
{
    /// <summary>
    /// The <c>client_id</c> that setup registers for the management console unless <see cref="ClientId"/> names another.
    /// </summary>
    public const string DefaultClientId = "mgmt-console";

    /// <summary>
    /// Gets or sets the requested, stable <c>client_id</c> of the management console. Defaults to
    /// <see cref="DefaultClientId"/>.
    /// </summary>
    public string ClientId { get; set; } = DefaultClientId;

    /// <summary>
    /// Gets or sets the management console's origin, an absolute <c>https</c> URL without a path, query or fragment.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the JSON text of the management console's public JWK Set, which setup registers by value.
    /// </summary>
    public string? Jwks { get; set; }

    /// <summary>
    /// Gets a value indicating whether the management console is configured, which is when <see cref="BaseUrl"/> or
    /// <see cref="Jwks"/> is set.
    /// </summary>
    public bool IsConfigured => BaseUrl is not null || Jwks is not null;
}
