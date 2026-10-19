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

using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Selects the credentials that protect a JWT the server addresses to a client — an ID token, a logout token, or a
/// UserInfo response. The JWT is signed with the tenant's key using the signature algorithm the client registers and
/// encrypted to the client's public key using the encryption algorithms the client registers; each registered algorithm
/// must be permitted by the client's effective settings, which carry the tenant's lists and the ceiling of every
/// security profile the client selects.
/// </summary>
// See ADR-0155.
internal interface IClientJwtCredentialSelector
{
    /// <summary>
    /// Selects the credentials that protect a JWT the server addresses to the specified client.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The client the JWT is addressed to.</param>
    /// <param name="protectionSettings">The settings that govern the protection of the kind of JWT.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the credentials; a
    /// response the client registered no algorithm for is unprotected.</returns>
    /// <exception cref="NCode.Identity.OpenId.Exceptions.OpenIdException">A <c>server_error</c> when the client's
    /// registration cannot be honored: a registered algorithm its effective settings do not permit, an encryption policy
    /// it registered no algorithm for, or no key for a registered algorithm. The response is never sent with weaker
    /// protection than the client registered.</exception>
    ValueTask<ClientJwtCredentials> SelectCredentialsAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ClientJwtProtectionSettings protectionSettings,
        CancellationToken cancellationToken
    );
}
