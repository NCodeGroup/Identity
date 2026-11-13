// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the management console's confidential client that the request to create the root administrator
/// registers in the same setup.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateRootManagementConsole
{
    /// <summary>
    /// Gets the requested, stable <c>client_id</c> of the management console. Defaults to
    /// <see cref="SetupManagementConsoleOptions.DefaultClientId"/>.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets the management console's origin, an absolute <c>https</c> URL without a path, query or fragment.
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Gets the management console's public JWK Set, a JSON object with a <c>keys</c> array, which setup registers by
    /// value.
    /// </summary>
    public required JsonElement Jwks { get; init; }

    /// <summary>
    /// Gets or sets the unrecognized members, so an unknown wire member round-trips instead of being dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
