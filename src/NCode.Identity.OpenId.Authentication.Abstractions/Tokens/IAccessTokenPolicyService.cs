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
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Decides the policy an access token is issued under. An access token is bound by its client and by every resource
/// server it targets: their settings combine by their lattice merges, the token must satisfy every requirement among
/// them, and its credentials are chosen within every bound — or the request is refused.
/// </summary>
/// <remarks>
/// <para>
/// Every <see cref="ITokenService"/> issues an access token under the policy this service evaluates, so an
/// implementation that replaces the default token service resolves it rather than reproduce the policy, and every
/// access-token setting it reads comes from <see cref="AccessTokenPolicy.Settings"/>.
/// </para>
/// <para>
/// Register a custom implementation to extend the policy — for example, to bound access tokens by the settings of a
/// further scope. A replacement must not weaken the bounds of the default implementation.
/// </para>
/// </remarks>
// See ADR-0158.
[PublicAPI]
public interface IAccessTokenPolicyService
{
    /// <summary>
    /// Evaluates the policy for an access token issued to <paramref name="openIdClient"/> for the resource servers in
    /// <paramref name="resolution"/>, bound by <paramref name="confirmation"/>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="openIdClient">The client the access token is issued to.</param>
    /// <param name="confirmation">The proof-of-possession binding the access token carries.</param>
    /// <param name="resolution">The resource servers the access token targets.</param>
    /// <returns>The <see cref="AccessTokenPolicy"/> the access token is issued under.</returns>
    /// <exception cref="NCode.Identity.OpenId.Exceptions.OpenIdException">The access token cannot be issued:
    /// <c>invalid_request</c> when the request cannot satisfy the policy (a required sender constraint is missing, or an
    /// encrypted token targets several resource servers and one of them decrypts its own tokens), and
    /// <c>server_error</c> when the configuration cannot (no tenant key, or no key of the targeted resource server, is
    /// permitted by the combined settings).</exception>
    AccessTokenPolicy Evaluate(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        TokenConfirmation confirmation,
        ResourceServerCredentialResolution resolution
    );
}
