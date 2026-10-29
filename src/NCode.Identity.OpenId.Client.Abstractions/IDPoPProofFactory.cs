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
/// Creates DPoP proofs (RFC 9449 §7) for requests that a caller sends to a resource server with a DPoP-bound access token.
/// </summary>
[PublicAPI]
public interface IDPoPProofFactory
{
    /// <summary>
    /// Creates a proof that carries the <c>ath</c> claim of the access token, signed by the key of the client's DPoP key ring
    /// with the specified JWK thumbprint, and carrying the last nonce recorded for the origin of the request URI.
    /// </summary>
    /// <param name="registration">The client that owns the DPoP key.</param>
    /// <param name="jwkThumbprint">The JWK thumbprint (<c>jkt</c>) of the key the access token is bound to.</param>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="requestUri">The absolute <c>http</c> or <c>https</c> URI of the request.</param>
    /// <param name="accessToken">The access token that the proof is bound to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    /// <exception cref="ArgumentException">
    /// An argument is empty, or <paramref name="requestUri"/> is not an absolute <c>http</c> or <c>https</c> URI.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The client has no DPoP key ring, or no key with the thumbprint.
    /// </exception>
    /// <exception cref="OpenIdProviderException">
    /// The provider's metadata cannot be retrieved, or it advertises no DPoP signing algorithm that the key can use.
    /// </exception>
    ValueTask<string> CreateResourceProofAsync(
        ClientRegistration registration,
        string jwkThumbprint,
        HttpMethod method,
        Uri requestUri,
        string accessToken,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Stores the <c>DPoP-Nonce</c> that a resource server returned, for the next proof to its origin. The caller retries a
    /// <c>use_dpop_nonce</c> challenge once.
    /// </summary>
    /// <param name="requestUri">The absolute <c>http</c> or <c>https</c> URI of the request that the server answered.</param>
    /// <param name="nonce">The nonce from the <c>DPoP-Nonce</c> response header.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="nonce"/> is empty, or <paramref name="requestUri"/> is not an absolute <c>http</c> or <c>https</c> URI.
    /// </exception>
    void RecordNonce(Uri requestUri, string nonce);
}
