// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the response to a setup token that is the current one.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CheckSetupTokenResponse
{
    /// <summary>
    /// Gets the management console that the deployment names for the wizard's console step, or <c>null</c> when the
    /// deployment names none.
    /// </summary>
    public SetupManagementConsoleHint? ManagementConsole { get; init; }

    /// <summary>
    /// Gets or sets the unrecognized members, so an unknown wire member round-trips instead of being dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
