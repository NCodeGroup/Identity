#region Copyright Preamble

// Copyright @ 2023 NCode Group
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
using NCode.Identity.OpenId.Endpoints;

namespace NCode.Identity.OpenId.Clients;

/// <summary>
/// Provides the ability to authenticate an OpenID client from the current HTTP request using various registered authentication methods.
/// </summary>
[PublicAPI]
public interface IClientAuthenticationService
{
    /// <summary>
    /// Attempts to authenticate an OpenID client from the current HTTP request.
    /// The authentication result may be undefined, contain a public client, a confidential client, or an error.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation,
    /// containing the <see cref="ClientAuthenticationResult"/> from the client authentication process.</returns>
    ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken);
}
