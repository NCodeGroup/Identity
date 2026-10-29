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

namespace NCode.Identity.OpenId.Client.ProofOfPossession;

/// <summary>
/// The default <see cref="IDPoPProofFactory"/>, which signs with the pinned key of the client's DPoP key ring.
/// </summary>
internal class DefaultDPoPProofFactory(
    IProviderMetadataResolver metadataResolver,
    DPoPKeyResolver keyResolver,
    DPoPNonceCache nonceCache
) : IDPoPProofFactory
{
    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;
    private DPoPKeyResolver KeyResolver { get; } = keyResolver;
    private DPoPNonceCache NonceCache { get; } = nonceCache;

    /// <inheritdoc />
    public async ValueTask<string> CreateResourceProofAsync(
        ClientRegistration registration,
        string jwkThumbprint,
        HttpMethod method,
        Uri requestUri,
        string accessToken,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrEmpty(jwkThumbprint);
        ArgumentNullException.ThrowIfNull(method);
        ValidateRequestUri(requestUri);
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        var metadata = await MetadataResolver.GetMetadataAsync(
            registration.Authority,
            cancellationToken
        );

        var key =
            KeyResolver.Resolve(registration, metadata, jwkThumbprint)
            ?? throw new InvalidOperationException(
                $"The client '{registration.ClientId}' cannot create a DPoP proof: the provider '{registration.Authority}' does not support DPoP with its key ring."
            );

        return key.CreateProof(method, requestUri, accessToken);
    }

    /// <inheritdoc />
    public void RecordNonce(Uri requestUri, string nonce)
    {
        ValidateRequestUri(requestUri);
        ArgumentException.ThrowIfNullOrEmpty(nonce);

        NonceCache.Set(requestUri, nonce);
    }

    private static void ValidateRequestUri(Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(requestUri);

        if (
            !requestUri.IsAbsoluteUri
            || (requestUri.Scheme != Uri.UriSchemeHttp && requestUri.Scheme != Uri.UriSchemeHttps)
        )
            throw new ArgumentException(
                "The request URI must be an absolute http or https URI.",
                nameof(requestUri)
            );
    }
}
