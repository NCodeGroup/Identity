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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Commands;

/// <summary>
/// Represents a mediator command that revokes a token on behalf of an authenticated client (RFC 7009). The matching
/// grant is invalidated; per the specification, an unknown or already-invalid token is treated as a successful no-op.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The authenticated <see cref="OpenIdClient"/> requesting the revocation.</param>
/// <param name="TokenRevocationRequest">The <see cref="ITokenRevocationRequest"/> identifying the token to revoke.</param>
[PublicAPI]
public readonly record struct RevokeTokenCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    ITokenRevocationRequest TokenRevocationRequest
) : ICommand;
