// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Processes authorization requests or continuations for the OpenID Connect authorization endpoint.
/// </summary>
public interface IAuthorizationRequestProcessor
{
    /// <summary>
    /// Processes an <c>OAuth</c> or <c>OpenID Connect</c> authorization request or continuation.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="authorizationRequest">The <see cref="IAuthorizationRequest"/> that represents the authorization request.</param>
    /// <param name="delivery">The <see cref="AuthorizationResponseDelivery"/> derived for the request: where, in which response mode, and with which state every response is delivered.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ReadOnlyEndpointDisposition"/> with the result of processing the request.</returns>
    ValueTask<ReadOnlyEndpointDisposition> ProcessRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        AuthorizationResponseDelivery delivery,
        CancellationToken cancellationToken
    );
}
