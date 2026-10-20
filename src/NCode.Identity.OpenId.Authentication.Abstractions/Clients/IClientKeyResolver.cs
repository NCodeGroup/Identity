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
using NCode.Identity.Secrets.Collections;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Resolves the effective set of keys for a client. A client's public keys come from exactly one source: the keys it
/// registered by value (its stored <see cref="OpenIdClient.SecretKeys"/>) or, when it has a <c>jwks_uri</c> setting,
/// the keys it publishes there, which replace any stored public key. The secrets a client shares with the server are
/// included either way. The collection is consumed by every path that uses a client's keys — verifying a client-signed
/// JWT (<c>private_key_jwt</c> and <c>client_secret_jwt</c> client authentication, signed request objects) and
/// encrypting a JWT addressed to the client.
/// </summary>
[PublicAPI]
public interface IClientKeyResolver
{
    /// <summary>
    /// Resolves the effective key collection for the specified client.
    /// </summary>
    /// <param name="openIdClient">The client whose keys to resolve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> containing the <see cref="ISecretKeyCollection"/>. When the client has no
    /// <c>jwks_uri</c>, its stored keys are returned unchanged; when its <c>jwks_uri</c> cannot be fetched or its key set
    /// is refused, only the secrets it shares with the server are returned.</returns>
    ValueTask<ISecretKeyCollection> ResolveSecretKeysAsync(
        OpenIdClient openIdClient,
        CancellationToken cancellationToken
    );
}
