#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Provides the logic for processing authorization requests or continuations for the OpenID Connect authorization endpoint.
/// </summary>
public interface IAuthorizationEndpointLogic
{
    /// <summary>
    /// Processes an <c>OAuth</c> or <c>OpenID Connect</c> authorization request or continuation.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="authorizationRequest">The <see cref="IAuthorizationRequest"/> that represents the authorization request.</param>
    /// <param name="clientRedirectContext">The <see cref="ClientRedirectContext"/> that contains information about how to redirect the user-agent.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ReadOnlyEndpointDisposition"/> with the result of processing the request.</returns>
    ValueTask<ReadOnlyEndpointDisposition> ProcessRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        ClientRedirectContext clientRedirectContext,
        CancellationToken cancellationToken
    );
}
