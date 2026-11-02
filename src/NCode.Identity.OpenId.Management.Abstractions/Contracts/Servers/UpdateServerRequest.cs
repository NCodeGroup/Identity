// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Servers;

/// <summary>
/// Represents the mutable metadata of an OpenID Server that a JSON Patch document may modify. Settings and
/// secrets are managed through their own endpoints.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateServerRequest
{
    /// <summary>
    /// Gets or sets the display name for the server.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}
