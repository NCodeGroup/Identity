﻿#region Copyright Preamble

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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using NIdentity.OpenId.Endpoints.Authorization.Messages;

namespace NIdentity.OpenId.Logic.Authorization;

/// <summary>
/// Provides the ability for the authorization server to generate claims for authorization requests.
/// </summary>
public interface IAuthorizationClaimsService
{
    /// <summary>
    /// Gets the claims that should be included in access tokens.
    /// </summary>
    /// <param name="authorizationRequestContext">The <see cref="AuthorizationRequestContext"/> for the authentication request.</param>
    /// <param name="authenticationTicket">The <see cref="AuthorizationRequestContext"/> for the authentication request.</param>
    /// <param name="timestamp">The <see cref="DateTimeOffset"/> when authorization occured.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> collection that contains <see cref="Claim"/> instances.</returns>
    IAsyncEnumerable<Claim> GetAccessTokenClaimsAsync(
        AuthorizationRequestContext authorizationRequestContext,
        AuthenticationTicket authenticationTicket,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the claims that should be included in ID tokens.
    /// </summary>
    /// <param name="authorizationRequestContext">The <see cref="AuthorizationRequestContext"/> for the authentication request.</param>
    /// <param name="authenticationTicket">The <see cref="AuthorizationRequestContext"/> for the authentication request.</param>
    /// <param name="timestamp">The <see cref="DateTimeOffset"/> when authorization occured.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> collection that contains <see cref="Claim"/> instances.</returns>
    IAsyncEnumerable<Claim> GetIdTokenClaimsAsync(
        AuthorizationRequestContext authorizationRequestContext,
        AuthenticationTicket authenticationTicket,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);
}
