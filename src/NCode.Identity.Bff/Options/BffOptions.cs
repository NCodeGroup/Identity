// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures the backend-for-frontend (BFF): the browser frontends it serves and the settings they share. Bound from
/// the <c>Bff</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class BffOptions
{
    /// <summary>
    /// Gets or sets the frontends that the BFF serves, by name. The configuration section is <c>Bff:Frontends</c>.
    /// </summary>
    public Dictionary<string, BffFrontendOptions> Frontends { get; set; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the anti-forgery header options. The configuration section is <c>Bff:Antiforgery</c>.
    /// </summary>
    public BffAntiforgeryOptions Antiforgery { get; set; } = new();

    /// <summary>
    /// Gets or sets the token refresh options. The configuration section is <c>Bff:Tokens</c>.
    /// </summary>
    public BffTokenOptions Tokens { get; set; } = new();
}
