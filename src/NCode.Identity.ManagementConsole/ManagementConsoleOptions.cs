// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Configures the management console host. The configuration section is <c>ManagementConsole</c>.
/// </summary>
[PublicAPI]
public sealed class ManagementConsoleOptions
{
    /// <summary>
    /// Gets or sets the absolute URL of the OP's management API base, such as <c>https://id.example.com/api</c>. The
    /// console forwards the browser's management API calls to it. The configuration key is
    /// <c>ManagementConsole:ManagementApiUrl</c>.
    /// </summary>
    public string? ManagementApiUrl { get; set; }

    /// <summary>
    /// Gets or sets the directory of the built <c>apps/mgmt</c> bundle, relative to the content root or absolute. The
    /// configuration key is <c>ManagementConsole:BundleRootPath</c>.
    /// </summary>
    public string? BundleRootPath { get; set; }

    /// <summary>
    /// Gets or sets the feature flags the console publishes to the bundle, keyed by feature name. The configuration
    /// key is <c>ManagementConsole:Features</c>.
    /// </summary>
    public Dictionary<string, bool> Features { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets how the console presents itself. The configuration key is <c>ManagementConsole:Branding</c>.
    /// </summary>
    public ManagementConsoleBrandingOptions Branding { get; set; } = new();
}
