// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenRevocationRequest"/> abstraction.
/// </summary>
public sealed class TokenRevocationRequest
    : OpenIdMessage<TokenRevocationRequest>,
        ITokenRevocationRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenRevocationRequest"/> class.
    /// </summary>
    public TokenRevocationRequest()
    {
        // nothing
    }

    private TokenRevocationRequest(TokenRevocationRequest other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public string? Token
    {
        get => GetKnownParameter(OpenIdCommonParameters.Token);
        set => SetKnownParameter(OpenIdCommonParameters.Token, value);
    }

    /// <inheritdoc />
    public string? TokenTypeHint
    {
        get => GetKnownParameter(OpenIdCommonParameters.TokenTypeHint);
        set => SetKnownParameter(OpenIdCommonParameters.TokenTypeHint, value);
    }

    /// <inheritdoc />
    public override TokenRevocationRequest Clone() => new(this);
}
