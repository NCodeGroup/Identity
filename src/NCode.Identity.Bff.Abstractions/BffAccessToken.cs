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

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Represents an access token that the BFF obtained for the current session.
/// </summary>
[PublicAPI]
public sealed class BffAccessToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffAccessToken"/> class.
    /// </summary>
    /// <param name="value">The access token value.</param>
    /// <param name="tokenType">The token type, such as <c>Bearer</c> or <c>DPoP</c>.</param>
    /// <param name="expiresAt">The instant the access token expires.</param>
    public BffAccessToken(string value, string tokenType, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentException.ThrowIfNullOrEmpty(tokenType);
        Value = value;
        TokenType = tokenType;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Gets the access token value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the token type, such as <c>Bearer</c> or <c>DPoP</c>.
    /// </summary>
    public string TokenType { get; }

    /// <summary>
    /// Gets the instant the access token expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets the <c>jkt</c> of the DPoP key the access token is bound to, or <see langword="null"/> when it is not
    /// sender-constrained.
    /// </summary>
    public string? DPoPJwkThumbprint { get; init; }
}
