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

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents a token revocation request (RFC 7009 §2.1).
/// </summary>
/// <param name="token">The token to revoke.</param>
[PublicAPI]
public sealed class TokenRevocationRequest(string token)
{
    /// <summary>
    /// Gets the token to revoke.
    /// </summary>
    public string Token { get; } = ValidateNotEmpty(token);

    /// <summary>
    /// Gets the hint about the type of the token, if any. See <see cref="TokenTypeHints"/>.
    /// </summary>
    public string? TokenTypeHint { get; init; }

    private static string ValidateNotEmpty(
        string value,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))]
            string? paramName = null
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        return value;
    }
}
