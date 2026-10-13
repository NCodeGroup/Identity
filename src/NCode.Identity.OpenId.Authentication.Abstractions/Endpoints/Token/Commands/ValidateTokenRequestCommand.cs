#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;

/// <summary>
/// Represents a mediator command that validates a token request against the protocol rules and the client's
/// configuration, before its grant is processed. A handler reports a validation failure as an OpenID error; a
/// successful validation completes without a result.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The authenticated <see cref="OpenIdClient"/> making the token request.</param>
/// <param name="TokenRequest">The <see cref="ITokenRequest"/> being validated.</param>
[PublicAPI]
public readonly record struct ValidateTokenRequestCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    ITokenRequest TokenRequest
) : ICommand;
