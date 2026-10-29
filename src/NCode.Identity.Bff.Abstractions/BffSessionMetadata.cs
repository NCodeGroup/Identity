#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
