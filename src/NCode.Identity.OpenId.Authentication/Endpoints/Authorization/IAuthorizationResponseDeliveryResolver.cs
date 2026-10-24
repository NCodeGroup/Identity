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

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Derives the <see cref="AuthorizationResponseDelivery"/> of an authorization request: the redirect URI checked
/// against the client's registration, the handler of the effective response mode, and the state to echo (ADR-0201).
/// </summary>
/// <remarks>
/// Each overload throws an <see cref="NCode.Identity.OpenId.Exceptions.OpenIdException"/> when the delivery cannot be
/// derived. Such an error is never redirected, because the response has nowhere safe to go.
/// </remarks>
internal interface IAuthorizationResponseDeliveryResolver
{
    /// <summary>
    /// Derives the delivery of a request as the user agent sent it, before any request object is loaded. The
    /// <c>state</c>, <c>response_mode</c> and <c>redirect_uri</c> are read from the request values, and the
    /// <c>redirect_uri</c> is checked against the client's registration.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that the request names.</param>
    /// <param name="requestValues">The <see cref="IRequestValues"/> of the request as the user agent sent it.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="AuthorizationResponseDelivery"/> of the request.</returns>
    ValueTask<AuthorizationResponseDelivery> ResolveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IRequestValues requestValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Derives the delivery of a loaded request. The <c>state</c>, <c>response_mode</c> and <c>redirect_uri</c> are
    /// read from the verified request object when there is one, and from the request message otherwise
    /// (RFC 9101 §6.3). The <c>redirect_uri</c> is checked against the client's registration.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that the request names.</param>
    /// <param name="authorizationRequest">The loaded <see cref="IAuthorizationRequest"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="AuthorizationResponseDelivery"/> of the request.</returns>
    ValueTask<AuthorizationResponseDelivery> ResolveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Derives the delivery of a loaded request that is delivered to a redirect URI that was already checked against
    /// the client's registration, such as the redirect URI of the outer request or of a parked request. The
    /// <c>state</c> and <c>response_mode</c> are read from the verified request object when there is one, and from the
    /// request message otherwise (RFC 9101 §6.3). The redirect URI is not checked again.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that the request names.</param>
    /// <param name="authorizationRequest">The loaded <see cref="IAuthorizationRequest"/>.</param>
    /// <param name="checkedRedirectUri">The <c>redirect_uri</c> that was already checked against the client's registration.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="AuthorizationResponseDelivery"/> of the request.</returns>
    ValueTask<AuthorizationResponseDelivery> ResolveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        string checkedRedirectUri,
        CancellationToken cancellationToken
    );
}
