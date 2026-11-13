// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the management console that the deployment names for the wizard's console step. It is only a hint: it
/// registers nothing.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SetupManagementConsoleHint
{
    /// <summary>
    /// Gets the https origin of the management console.
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Gets the <c>client_id</c> that the console step proposes for the management console's client.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets or sets the unrecognized members, so an unknown wire member round-trips instead of being dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
