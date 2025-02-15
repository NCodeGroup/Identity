#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Results;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationTicket"/> abstraction.
/// </summary>
[PublicAPI]
public class AuthorizationTicket :
    OpenIdMessage<AuthorizationTicket>,
    IAuthorizationTicket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationTicket"/> class.
    /// </summary>
    public AuthorizationTicket()
    {
        // nothing
    }

    /// <inheritdoc />
    protected AuthorizationTicket(AuthorizationTicket other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public DateTimeOffset CreatedWhen
    {
        get => GetKnownParameter(KnownParameters.CreatedWhen);
        set => SetKnownParameter(KnownParameters.CreatedWhen, value);
    }

    /// <inheritdoc />
    public string? State
    {
        get => GetKnownParameter(KnownParameters.State);
        set => SetKnownParameter(KnownParameters.State, value);
    }

    /// <inheritdoc />
    public string? AuthorizationCode
    {
        get => GetKnownParameter(KnownParameters.AuthorizationCode);
        set => SetKnownParameter(KnownParameters.AuthorizationCode, value);
    }

    /// <inheritdoc />
    public string? IdToken
    {
        get => GetKnownParameter(KnownParameters.IdToken);
        set => SetKnownParameter(KnownParameters.IdToken, value);
    }

    /// <inheritdoc />
    public string? AccessToken
    {
        get => GetKnownParameter(KnownParameters.AccessToken);
        set => SetKnownParameter(KnownParameters.AccessToken, value);
    }

    /// <inheritdoc />
    public string? TokenType
    {
        get => GetKnownParameter(KnownParameters.TokenType);
        set => SetKnownParameter(KnownParameters.TokenType, value);
    }

    /// <inheritdoc />
    public TimeSpan? ExpiresIn
    {
        get => GetKnownParameter(KnownParameters.ExpiresIn);
        set => SetKnownParameter(KnownParameters.ExpiresIn, value);
    }

    /// <inheritdoc />
    public string? Issuer
    {
        get => GetKnownParameter(KnownParameters.Issuer);
        set => SetKnownParameter(KnownParameters.Issuer, value);
    }

    /// <inheritdoc />
    public override AuthorizationTicket Clone() => new(this);
}
