// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the options used to configure the OpenID server.
/// </summary>
[PublicAPI]
public sealed class OpenIdPlatformOptions
{
    /// <summary>
    /// Gets or sets the period of time after which the server's persisted settings are read again.
    /// A committed change is read at once on every replica that receives its announcement, so the period bounds a
    /// replica that missed it. The default value is 5 minutes.
    /// </summary>
    public TimeSpan SettingsPeriodicRefreshInterval { get; set; } = TimeSpan.FromMinutes(5.0);

    /// <summary>
    /// Gets or sets the configuration section that holds the server's settings, keyed by setting name (for example
    /// <c>grant_types_supported</c>). These are the root of the settings hierarchy that every tenant and client
    /// inherits from.
    /// </summary>
    /// <remarks>
    /// The section is carried as-is rather than bound to a fixed shape: each child is a setting typed by its registered
    /// setting descriptor, and the section is watched so a configuration reload refreshes them. Setting names are a
    /// closed vocabulary — a name with no registered descriptor, or a value that does not convert to the descriptor's
    /// type, stops the host when it starts; a reload that cannot be honored leaves the previous settings in force.
    /// When unset, the server has no configured settings beyond the defaults.
    /// </remarks>
    public IConfigurationSection? Settings { get; set; }
}
