// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The plaintext metadata of a <c>Backend for Frontend (BFF)</c> session. It holds what the store needs to expire
/// and index the session, and never a token, a claim or a secret.
/// </summary>
[PublicAPI]
public sealed class BffSessionMetadata
{
    /// <summary>
    /// Gets the name of the frontend that owns the session.
    /// </summary>
    public string Frontend { get; init; } = string.Empty;

    /// <summary>
    /// Gets the issuer of the OpenID provider that authenticated the user.
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the subject identifier of the user.
    /// </summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// Gets the session identifier (<c>sid</c>) that the OpenID provider assigned, if any.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the instant when the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the instant when the session was last written for activity.
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; init; }

    /// <summary>
    /// Gets the instant when the session ends regardless of activity.
    /// </summary>
    public DateTimeOffset AbsoluteExpiresAt { get; init; }

    /// <summary>
    /// Gets the instant when the session ends unless there is activity before it.
    /// </summary>
    public DateTimeOffset IdleExpiresAt { get; init; }

    /// <summary>
    /// Gets or sets the members that this version of the type does not know, so that they survive a round trip.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
