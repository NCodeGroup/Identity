// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

        return await key.CreateProofAsync(method, requestUri, accessToken, cancellationToken);
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
