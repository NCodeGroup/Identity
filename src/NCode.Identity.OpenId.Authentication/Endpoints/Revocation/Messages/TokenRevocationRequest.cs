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
