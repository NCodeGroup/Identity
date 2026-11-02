// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Configures how <strong>development-only</strong> signing-key conveniences apply to the OpenID server. Bound from the
/// <c>DeveloperKeys</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class DeveloperKeysOptions
{
    /// <summary>
    /// The configuration section (<c>DeveloperKeys</c>) bound to <see cref="DeveloperKeysOptions"/>.
    /// </summary>
    public const string DefaultSectionName = "DeveloperKeys";

    /// <summary>
    /// Gets or sets a value indicating whether a tenant signing key that can no longer be read is removed. A tenant key
    /// can no longer be read when its master key, or the root key that wraps it, was lost or changed. It is removed when
    /// the tenant is seeded, and provisioning replaces it (ADR-1313). Development-only: Production refuses the section.
    /// </summary>
    public bool PruneUnreadableKeys { get; set; }
}
