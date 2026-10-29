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

using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.DPoP;

namespace NCode.Identity.OpenId.Client.ProofOfPossession;

/// <summary>
/// A DPoP signing key chosen for one operation, with the JWK thumbprint (<c>jkt</c>) tokens are bound to.
/// </summary>
internal sealed class DPoPProofKey(
    IDPoPProofService proofService,
    DPoPNonceCache nonceCache,
    JoseSigningCredentials credentials,
    string jwkThumbprint
)
{
    private IDPoPProofService ProofService { get; } = proofService;
    private DPoPNonceCache NonceCache { get; } = nonceCache;

    public JoseSigningCredentials Credentials { get; } = credentials;

    public string JwkThumbprint { get; } = jwkThumbprint;

    public string CreateProof(HttpMethod method, Uri requestUri, string? accessToken) =>
        ProofService.CreateProof(
            new DPoPProofCreationOptions
            {
                SigningCredentials = Credentials,
                HttpMethod = method.Method,
                HttpUri = requestUri.AbsoluteUri,
                AccessToken = accessToken,
                Nonce = NonceCache.Get(requestUri),
            }
        );

    public void RecordNonce(Uri requestUri, string nonce) => NonceCache.Set(requestUri, nonce);
}
