// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

    public ValueTask<string> CreateProofAsync(
        HttpMethod method,
        Uri requestUri,
        string? accessToken,
        CancellationToken cancellationToken
    ) =>
        ProofService.CreateProofAsync(
            new DPoPProofCreationOptions
            {
                SigningCredentials = Credentials,
                HttpMethod = method.Method,
                HttpUri = requestUri.AbsoluteUri,
                AccessToken = accessToken,
                Nonce = NonceCache.Get(requestUri),
            },
            cancellationToken
        );

    public void RecordNonce(Uri requestUri, string nonce) => NonceCache.Set(requestUri, nonce);
}
