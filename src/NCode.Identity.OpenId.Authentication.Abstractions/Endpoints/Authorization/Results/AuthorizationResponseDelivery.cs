#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Where and how an authorization response, a success or an error, is delivered: the redirect URI checked against the
/// client's registration, the handler of the effective response mode, and the state to echo. The server derives it
/// once per request.
/// </summary>
/// <param name="RedirectUri">The <c>redirect_uri</c> the response is returned to, exactly as the request wrote it and the client registered it.</param>
/// <param name="ResponseModeHandler">The <see cref="IAuthorizationResponseModeHandler"/> of the effective <c>response_mode</c> that delivers the response to the user-agent.</param>
/// <param name="State">The <c>state</c> parameter that is included when returning the response to the user-agent.</param>
// See ADR-0201.
[PublicAPI]
public sealed record AuthorizationResponseDelivery(
    string RedirectUri,
    IAuthorizationResponseModeHandler ResponseModeHandler,
    string? State
)
{
    /// <summary>
    /// Gets the credentials that secure the response when
    /// <see cref="IAuthorizationResponseModeHandler.SecuredResponseMode"/> of the response mode handler is set,
    /// selected when the delivery is derived; <see langword="null"/> otherwise.
    /// </summary>
    public ClientJwtCredentials? SecuringCredentials { get; init; }
}
