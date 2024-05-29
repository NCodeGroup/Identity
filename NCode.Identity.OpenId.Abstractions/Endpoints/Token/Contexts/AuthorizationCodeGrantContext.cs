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

using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Token.Messages;

namespace NCode.Identity.OpenId.Endpoints.Token.Contexts;

/// <summary>
/// Contains contextual information about the request that the authorization server will use to issue tokens using the <c>Authorization Code</c> grant type.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> instance for the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> instance that represents the client application that is requesting the token.</param>
/// <param name="TokenRequest">The <see cref="ITokenRequest"/> instance that represents the token request.</param>
/// <param name="AuthorizationRequest">The <see cref="IAuthorizationRequest"/> instance that represents the original authorization request.</param>
public readonly record struct AuthorizationCodeGrantContext(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    ITokenRequest TokenRequest,
    IAuthorizationRequest AuthorizationRequest
);
