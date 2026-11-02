// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    public override string GrantType => OpenIdProtocol.GrantTypes.RefreshToken;

    /// <summary>
    /// Gets the refresh token.
    /// </summary>
    public string RefreshToken { get; } = ValidateNotEmpty(refreshToken);

    /// <inheritdoc />
    public override void AddGrantParameters(ICollection<KeyValuePair<string, string>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Add(
            new KeyValuePair<string, string>(OpenIdProtocol.Parameters.RefreshToken, RefreshToken)
        );
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
