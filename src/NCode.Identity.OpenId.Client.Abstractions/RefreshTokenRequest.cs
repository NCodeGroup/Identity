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
/// Represents a refresh token grant request.
/// </summary>
/// <param name="refreshToken">The refresh token.</param>
[PublicAPI]
public sealed class RefreshTokenRequest(string refreshToken) : TokenRequest
{
    /// <inheritdoc />
    public override string GrantType => "refresh_token";

    /// <summary>
    /// Gets the refresh token.
    /// </summary>
    public string RefreshToken { get; } = ValidateNotEmpty(refreshToken);

    /// <inheritdoc />
    public override void AddGrantParameters(ICollection<KeyValuePair<string, string>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Add(new KeyValuePair<string, string>("refresh_token", RefreshToken));
    }

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
