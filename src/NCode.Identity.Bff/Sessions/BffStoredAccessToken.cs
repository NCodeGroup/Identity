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

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// An access token that the session caches, as stored in the encrypted session payload.
/// </summary>
internal sealed record BffStoredAccessToken
{
    /// <summary>
    /// Gets the name of the token requirement that the token satisfies.
    /// </summary>
    public string Requirement { get; init; } = string.Empty;

    /// <summary>
    /// Gets the resource indicator that the token was requested for, if any.
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// Gets the scope that the token was requested with, if any.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Gets the access token value.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Gets the token type, such as <c>Bearer</c> or <c>DPoP</c>.
    /// </summary>
    public string TokenType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the instant when the token expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the JWK thumbprint that the token is bound to, if it is DPoP-bound.
    /// </summary>
    public string? DPoPJwkThumbprint { get; init; }
}
