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

using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Results;

namespace NCode.Identity.OpenId.Logic.Authorization;

/// <summary>
/// Provides the ability for the authorization server to generate grants in response to authorization requests.
/// </summary>
public interface IAuthorizationTicketService
{
    /// <summary>
    /// Generates the authorization code for an authorization request.
    /// </summary>
    /// <param name="command">The <see cref="CreateAuthorizationTicketCommand"/> for the current authorization request.</param>
    /// <param name="ticket">The <see cref="IAuthorizationTicket"/> where the authorization code will be stored.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask CreateAuthorizationCodeAsync(
        CreateAuthorizationTicketCommand command,
        IAuthorizationTicket ticket,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generates the access token for an authorization request.
    /// </summary>
    /// <param name="command">The <see cref="CreateAuthorizationTicketCommand"/> for the current authorization request.</param>
    /// <param name="ticket">The <see cref="IAuthorizationTicket"/> where the access token will be stored.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask CreateAccessTokenAsync(
        CreateAuthorizationTicketCommand command,
        IAuthorizationTicket ticket,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generates the ID token for an authorization request.
    /// </summary>
    /// <param name="command">The <see cref="CreateAuthorizationTicketCommand"/> for the current authorization request.</param>
    /// <param name="ticket">The <see cref="IAuthorizationTicket"/> where the ID token will be stored.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask CreateIdTokenAsync(
        CreateAuthorizationTicketCommand command,
        IAuthorizationTicket ticket,
        CancellationToken cancellationToken);
}
