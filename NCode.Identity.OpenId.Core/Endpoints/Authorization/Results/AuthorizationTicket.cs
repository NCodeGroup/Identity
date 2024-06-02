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

namespace NCode.Identity.OpenId.Endpoints.Authorization.Results;

internal class AuthorizationTicketProperties
{
    public DateTimeOffset CreatedWhen { get; set; }
}

internal class AuthorizationTicket :
    OpenIdMessage<AuthorizationTicket, AuthorizationTicketProperties>,
    IAuthorizationTicket
{
    /// <inheritdoc />
    public DateTimeOffset CreatedWhen
    {
        get => Properties.CreatedWhen;
        set => Properties.CreatedWhen = value;
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
}
