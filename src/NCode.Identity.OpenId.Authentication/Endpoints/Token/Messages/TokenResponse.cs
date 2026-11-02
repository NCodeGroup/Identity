// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenResponse"/> abstraction.
/// </summary>
public sealed class TokenResponse : OpenIdMessage<TokenResponse>, ITokenResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenResponse"/> class.
    /// </summary>
    public TokenResponse()
    {
        // nothing
    }

    /// <inheritdoc />
    private TokenResponse(TokenResponse other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public string? AccessToken
    {
        get => GetKnownParameter(OpenIdCommonParameters.AccessToken);
        set => SetKnownParameter(OpenIdCommonParameters.AccessToken, value);
    }

    /// <inheritdoc />
    public TimeSpan? ExpiresIn
    {
        get => GetKnownParameter(OpenIdCommonParameters.ExpiresIn);
        set => SetKnownParameter(OpenIdCommonParameters.ExpiresIn, value);
    }

    /// <inheritdoc />
    public string? IdToken
    {
        get => GetKnownParameter(OpenIdCommonParameters.IdToken);
        set => SetKnownParameter(OpenIdCommonParameters.IdToken, value);
    }

    /// <inheritdoc />
    public string? RefreshToken
    {
        get => GetKnownParameter(OpenIdCommonParameters.RefreshToken);
        set => SetKnownParameter(OpenIdCommonParameters.RefreshToken, value);
    }

    /// <inheritdoc />
    public TimeSpan? RefreshTokenExpiresIn
    {
        get => GetKnownParameter(OpenIdCommonParameters.RefreshTokenExpiresIn);
        set => SetKnownParameter(OpenIdCommonParameters.RefreshTokenExpiresIn, value);
    }

    /// <inheritdoc />
    public List<string>? Scopes
    {
        get => GetKnownParameter(OpenIdCommonParameters.Scopes);
        set => SetKnownParameter(OpenIdCommonParameters.Scopes, value);
    }

    /// <inheritdoc />
    public string? TokenType
    {
        get => GetKnownParameter(OpenIdCommonParameters.TokenType);
        set => SetKnownParameter(OpenIdCommonParameters.TokenType, value);
    }

    /// <inheritdoc />
    public string? IssuedTokenType
    {
        get => GetKnownParameter(OpenIdCommonParameters.IssuedTokenType);
        set => SetKnownParameter(OpenIdCommonParameters.IssuedTokenType, value);
    }

    /// <inheritdoc />
    public override TokenResponse Clone() => new(this);
}
