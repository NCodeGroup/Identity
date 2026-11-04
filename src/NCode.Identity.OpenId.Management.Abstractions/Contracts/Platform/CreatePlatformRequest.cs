// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Platform;

/// <summary>
/// Represents the request body to create a new OpenID Server.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreatePlatformRequest
{
    /// <summary>
    /// Gets the display name for the server.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the initial JSON settings for the server.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
