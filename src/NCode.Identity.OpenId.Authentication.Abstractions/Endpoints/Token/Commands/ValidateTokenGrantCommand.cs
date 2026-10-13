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
/// Represents a mediator command that validates a persisted grant of type <typeparamref name="TGrant"/> against the
/// current token request — confirming it is active, unexpired, bound to this client, and otherwise redeemable. A
/// handler reports a validation failure as an OpenID error; a successful validation completes without a result.
/// </summary>
/// <typeparam name="TGrant">The type of grant being validated (for example, an authorization-code or refresh-token grant).</typeparam>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The authenticated <see cref="OpenIdClient"/> making the token request.</param>
/// <param name="TokenRequest">The <see cref="ITokenRequest"/> redeeming the grant.</param>
/// <param name="Grant">The persisted grant being validated.</param>
[PublicAPI]
public readonly record struct ValidateTokenGrantCommand<TGrant>(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    ITokenRequest TokenRequest,
    TGrant Grant
) : ICommand;
