// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.ManagementConsole.Options;

/// <summary>
/// Configures how the management console presents itself. Every setting is optional, and the bundle supplies its own
/// default for each one that is not set.
/// </summary>
[PublicAPI]
public sealed class ManagementConsoleBrandingOptions
{
    /// <summary>
    /// Gets or sets the name the console shows for itself. The configuration key is
    /// <c>ManagementConsole:Branding:DisplayName</c>.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the absolute <c>https</c> URL of the console's logo. The configuration key is
    /// <c>ManagementConsole:Branding:LogoUrl</c>.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the absolute <c>https</c> URL of the icon the browser shows in the console's tab, in place of the
    /// family's. The configuration key is <c>ManagementConsole:Branding:FaviconUrl</c>.
    /// </summary>
    public string? FaviconUrl { get; set; }

    /// <summary>
    /// Gets or sets the console's primary color. The configuration key is
    /// <c>ManagementConsole:Branding:PrimaryColor</c>.
    /// </summary>
    public string? PrimaryColor { get; set; }

    /// <summary>
    /// Gets or sets the console's secondary color. The configuration key is
    /// <c>ManagementConsole:Branding:SecondaryColor</c>.
    /// </summary>
    public string? SecondaryColor { get; set; }

    /// <summary>
    /// Gets or sets the console's color mode, <c>light</c> or <c>dark</c>. The configuration key is
    /// <c>ManagementConsole:Branding:Mode</c>.
    /// </summary>
    public string? Mode { get; set; }
}
