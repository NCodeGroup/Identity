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

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationTicket"/> abstraction.
/// </summary>
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
        get => GetKnownParameter(CoreParameters.CreatedWhen);
        set => SetKnownParameter(CoreParameters.CreatedWhen, value);
    }

    /// <inheritdoc />
    public string? State
    {
        get => GetKnownParameter(CoreParameters.State);
        set => SetKnownParameter(CoreParameters.State, value);
    }

    /// <inheritdoc />
    public string? AuthorizationCode
    {
        get => GetKnownParameter(CoreParameters.AuthorizationCode);
        set => SetKnownParameter(CoreParameters.AuthorizationCode, value);
    }

    /// <inheritdoc />
    public string? IdToken
    {
        get => GetKnownParameter(CoreParameters.IdToken);
        set => SetKnownParameter(CoreParameters.IdToken, value);
    }

    /// <inheritdoc />
    public string? AccessToken
    {
        get => GetKnownParameter(CoreParameters.AccessToken);
        set => SetKnownParameter(CoreParameters.AccessToken, value);
    }

    /// <inheritdoc />
    public string? TokenType
    {
        get => GetKnownParameter(CoreParameters.TokenType);
        set => SetKnownParameter(CoreParameters.TokenType, value);
    }

    /// <inheritdoc />
    public TimeSpan? ExpiresIn
    {
        get => GetKnownParameter(CoreParameters.ExpiresIn);
        set => SetKnownParameter(CoreParameters.ExpiresIn, value);
    }

    /// <inheritdoc />
    public string? Issuer
    {
        get => GetKnownParameter(CoreParameters.Issuer);
        set => SetKnownParameter(CoreParameters.Issuer, value);
    }

    /// <inheritdoc />
    public override AuthorizationTicket Clone() => new(this);
}
