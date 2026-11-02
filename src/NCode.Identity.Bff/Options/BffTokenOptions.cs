// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures how the BFF keeps the access token of a session fresh.
/// </summary>
[PublicAPI]
public sealed class BffTokenOptions
{
    /// <summary>
    /// Gets or sets how long before it expires an access token is refreshed. The default is 60 seconds. The
    /// configuration key is <c>Bff:Tokens:RefreshBeforeExpiry</c>.
    /// </summary>
    public TimeSpan RefreshBeforeExpiry { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets how long the cluster-wide refresh lease lasts. The default is 40 seconds. It must be at least twice
    /// the protocol client's request timeout plus 10 seconds, which <c>AddIdentityBff</c> validates. The configuration
    /// key is <c>Bff:Tokens:RefreshLeaseDuration</c>.
    /// </summary>
    public TimeSpan RefreshLeaseDuration { get; set; } = TimeSpan.FromSeconds(40);
}
