// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationTicket"/> abstraction.
/// </summary>
public sealed class AuthorizationTicket : OpenIdMessage<AuthorizationTicket>, IAuthorizationTicket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationTicket"/> class.
    /// </summary>
    public AuthorizationTicket()
    {
        // nothing
    }

    /// <inheritdoc />
    private AuthorizationTicket(AuthorizationTicket other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public DateTimeOffset CreatedWhen
    {
        get => GetKnownParameter(OpenIdCommonParameters.CreatedWhen);
        set => SetKnownParameter(OpenIdCommonParameters.CreatedWhen, value);
    }

    /// <inheritdoc />
    public string? State
    {
        get => GetKnownParameter(OpenIdCommonParameters.State);
        set => SetKnownParameter(OpenIdCommonParameters.State, value);
    }

    /// <inheritdoc />
    public string? SessionState
    {
        get => GetKnownParameter(OpenIdCommonParameters.SessionState);
        set => SetKnownParameter(OpenIdCommonParameters.SessionState, value);
    }

    /// <inheritdoc />
    public string? AuthorizationCode
    {
        get => GetKnownParameter(OpenIdCommonParameters.AuthorizationCode);
        set => SetKnownParameter(OpenIdCommonParameters.AuthorizationCode, value);
    }

    /// <inheritdoc />
    public string? IdToken
    {
        get => GetKnownParameter(OpenIdCommonParameters.IdToken);
        set => SetKnownParameter(OpenIdCommonParameters.IdToken, value);
    }

    /// <inheritdoc />
    public string? AccessToken
    {
        get => GetKnownParameter(OpenIdCommonParameters.AccessToken);
        set => SetKnownParameter(OpenIdCommonParameters.AccessToken, value);
    }

    /// <inheritdoc />
    public string? TokenType
    {
        get => GetKnownParameter(OpenIdCommonParameters.TokenType);
        set => SetKnownParameter(OpenIdCommonParameters.TokenType, value);
    }

    /// <inheritdoc />
    public TimeSpan? ExpiresIn
    {
        get => GetKnownParameter(OpenIdCommonParameters.ExpiresIn);
        set => SetKnownParameter(OpenIdCommonParameters.ExpiresIn, value);
    }

    /// <inheritdoc />
    public string? Issuer
    {
        get => GetKnownParameter(OpenIdCommonParameters.Issuer);
        set => SetKnownParameter(OpenIdCommonParameters.Issuer, value);
    }

    /// <inheritdoc />
    public override AuthorizationTicket Clone() => new(this);
}
