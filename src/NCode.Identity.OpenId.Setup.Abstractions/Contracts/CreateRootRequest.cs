// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the request body to create the root administrator. The password is supplied as plaintext over the
/// (TLS-protected) wire and is hashed server-side; it is never persisted or returned.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateRootRequest
{
    /// <summary>
    /// Gets the setup token that authorizes the request.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Gets the root administrator's username.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the root administrator's email address, or <c>null</c> when none is recorded.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Gets the root administrator's password, hashed server-side on create.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// Gets the management console's client to register in the same setup, or <c>null</c> when the request registers
    /// no console client.
    /// </summary>
    public CreateRootManagementConsole? ManagementConsole { get; init; }

    /// <summary>
    /// Gets or sets the unrecognized members, so an unknown wire member round-trips instead of being dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
