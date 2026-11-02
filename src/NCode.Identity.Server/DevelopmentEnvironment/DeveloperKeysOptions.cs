// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Configures how <strong>development-only</strong> signing keys are provided to the OpenID server. Bound from the
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
    /// Gets or sets the <see cref="DeveloperKeyMode"/> that selects how signing keys are provided, or <c>null</c> when
    /// unset, which selects <see cref="DeveloperKeyMode.Ephemeral"/>. A Production deployment must state
    /// <see cref="DeveloperKeyMode.None"/> explicitly.
    /// </summary>
    public DeveloperKeyMode? Mode { get; set; }
}
