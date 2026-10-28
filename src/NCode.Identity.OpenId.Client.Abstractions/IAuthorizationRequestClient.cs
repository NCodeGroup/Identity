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

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Creates the URL that starts an authorization-code flow at the provider's authorization endpoint, pushing the request
/// (RFC 9126) whenever the provider advertises a pushed authorization request endpoint.
/// </summary>
/// <remarks>
/// An OAuth error response from the pushed authorization request endpoint is returned as data in
/// <see cref="AuthorizationRequestResult.Error"/>. A provider without an acceptable authorization endpoint, without
/// <c>S256</c> support, or without a pushed authorization request endpoint when one is required, and any other failure,
/// throws <see cref="OpenIdProviderException"/>.
/// </remarks>
[PublicAPI]
public interface IAuthorizationRequestClient
{
    /// <summary>
    /// Creates the URL that starts an authorization-code flow.
    /// </summary>
    /// <param name="registration">The client making the request.</param>
    /// <param name="request">The authorization request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask<AuthorizationRequestResult> CreateAuthorizationUriAsync(
        ClientRegistration registration,
        AuthorizationRequest request,
        CancellationToken cancellationToken
    );
}
