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
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Commands;

/// <summary>
/// Represents a mediator command that contributes claims to the JWT payload (body) of an access token being issued.
/// Each registered handler adds entries to the shared <paramref name="PayloadClaims"/> dictionary.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The <see cref="OpenIdClient"/> the access token is issued to.</param>
/// <param name="TokenContext">The <see cref="SecurityTokenContext"/> describing the access token being generated.</param>
/// <param name="PayloadClaims">The mutable claim set each handler contributes payload claims to.</param>
[PublicAPI]
public readonly record struct GetAccessTokenPayloadClaimsCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    SecurityTokenContext TokenContext,
    IDictionary<string, object> PayloadClaims
) : ICommand;
