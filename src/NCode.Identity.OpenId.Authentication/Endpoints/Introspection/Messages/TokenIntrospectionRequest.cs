// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenIntrospectionRequest"/> abstraction.
/// </summary>
public sealed class TokenIntrospectionRequest
    : OpenIdMessage<TokenIntrospectionRequest>,
        ITokenIntrospectionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenIntrospectionRequest"/> class.
    /// </summary>
    public TokenIntrospectionRequest()
    {
        // nothing
    }

    private TokenIntrospectionRequest(TokenIntrospectionRequest other)
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
    public override TokenIntrospectionRequest Clone() => new(this);
}
