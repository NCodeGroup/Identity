// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the setup page's view of an uninitialized instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SetupStatusResponse
{
    /// <summary>
    /// Gets the source of the setup token, one of the <see cref="SetupTokenSources"/> values.
    /// </summary>
    public required string TokenSource { get; init; }

    /// <summary>
    /// Gets the minimum length of the root administrator's password.
    /// </summary>
    public required int MinimumPasswordLength { get; init; }

    /// <summary>
    /// Gets or sets the unrecognized members, so an unknown wire member round-trips instead of being dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
