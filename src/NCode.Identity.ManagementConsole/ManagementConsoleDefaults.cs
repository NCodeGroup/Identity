// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Provides the well-known names and paths of the management console.
/// </summary>
[PublicAPI]
public static class ManagementConsoleDefaults
{
    /// <summary>
    /// The name of the configuration section that holds the console's options.
    /// </summary>
    public const string ConfigurationSectionName = "ManagementConsole";

    /// <summary>
    /// The name of the console's BFF frontend.
    /// </summary>
    public const string FrontendName = "mgmt";

    /// <summary>
    /// The base path of the management API as the browser sees it on the console's origin.
    /// </summary>
    public const string ApiBasePath = "/management";

    /// <summary>
    /// The path under which the console forwards requests to the management API.
    /// </summary>
    public const string ApiPath = "/management/api";

    /// <summary>
    /// The resource indicator of the management API.
    /// </summary>
    public const string ManagementApiResource = "urn:ncode:management";

    /// <summary>
    /// The path of the console's bootstrap document.
    /// </summary>
    public const string BootstrapPath = "/config/bootstrap.json";

    /// <summary>
    /// The path under which the console serves the bundle's static assets.
    /// </summary>
    public const string AssetsPath = "/assets";

    /// <summary>
    /// The placeholder in the bundle's page that the console replaces with a fresh content-security-policy nonce.
    /// </summary>
    public const string NoncePlaceholder = "__NCODE_CSP_NONCE__";
}
