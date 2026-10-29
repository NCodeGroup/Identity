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

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.DPoP;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.OpenId.Client.ProofOfPossession;

/// <summary>
/// Chooses the DPoP signing key of a client for one operation: from the client's <c>DPoPKeyRing</c>, optionally pinned by
/// JWK thumbprint, and with an algorithm the provider advertises in <c>dpop_signing_alg_values_supported</c>. DPoP is used
/// whenever it is available and is required when the client registration demands it or a key is pinned (RFC 9449,
/// ADR-0286, design D7).
/// </summary>
internal class DPoPKeyResolver(
    IClientKeyRingProvider keyRingProvider,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ICredentialSelector credentialSelector,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    IDPoPProofService proofService,
    DPoPNonceCache nonceCache
)
{
    private const string NoneAlgorithm = "none";

    private static readonly string[] DefaultPreferredAlgorithms =
    [
        AlgorithmCodes.DigitalSignature.EcdsaSha256,
        AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
        AlgorithmCodes.DigitalSignature.EcdsaSha384,
        AlgorithmCodes.DigitalSignature.RsaSsaPssSha384,
        AlgorithmCodes.DigitalSignature.EcdsaSha512,
        AlgorithmCodes.DigitalSignature.RsaSsaPssSha512,
    ];

    private IClientKeyRingProvider KeyRingProvider { get; } = keyRingProvider;
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;
    private ICredentialSelector CredentialSelector { get; } = credentialSelector;
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;
    private IDPoPProofService ProofService { get; } = proofService;
    private DPoPNonceCache NonceCache { get; } = nonceCache;

    /// <summary>
    /// Resolves the DPoP key for the client, or <see langword="null"/> when DPoP is neither required nor available.
    /// </summary>
    public DPoPProofKey? Resolve(
        ClientRegistration registration,
        OpenIdProviderMetadata metadata,
        string? jwkThumbprint
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(metadata);

        var id = registration.ClientId;
        var ring = registration.DPoPKeyRing;
        var authority = registration.Authority;
        var required = registration.DPoPBoundAccessTokens || jwkThumbprint is not null;

        if (string.IsNullOrEmpty(ring))
        {
            if (!required)
                return null;

            throw new InvalidOperationException(
                $"The client '{id}' must use DPoP but has no DPoP key ring."
            );
        }

        var keys = KeyRingProvider
            .GetKeyRing(ring)
            .OfType<AsymmetricSecretKey>()
            .Cast<SecretKey>()
            .ToList();
        if (keys.Count == 0)
            throw new InvalidOperationException(
                $"The DPoP key ring '{ring}' of the client '{id}' has no asymmetric key."
            );

        if (jwkThumbprint is not null)
        {
            keys = keys.Where(key =>
                    string.Equals(ComputeThumbprint(key), jwkThumbprint, StringComparison.Ordinal)
                )
                .ToList();
            if (keys.Count == 0)
                throw new InvalidOperationException(
                    $"The DPoP key ring '{ring}' of the client '{id}' has no key with the JWK thumbprint '{jwkThumbprint}'."
                );
        }

        var supported = metadata.DPoPSigningAlgValuesSupported;
        if (supported is null || supported.Count == 0)
        {
            if (!required)
                return null;

            throw new OpenIdProviderException(
                $"The client '{id}' must use DPoP, but the provider '{authority}' advertises no dpop_signing_alg_values_supported."
            );
        }

        if (
            !CredentialSelector.TryGetSigningCredentials(
                AlgorithmCollectionProvider.Collection,
                BuildPreferredAlgorithms(supported),
                keys,
                out var credentials
            )
        )
        {
            if (!required)
                return null;

            throw new OpenIdProviderException(
                $"The provider '{authority}' advertises no DPoP signing algorithm that the key ring '{ring}' of the client '{id}' can use."
            );
        }

        var thumbprint =
            ComputeThumbprint(credentials.SecretKey)
            ?? throw new InvalidOperationException(
                $"The public key of the DPoP key '{credentials.SecretKey.KeyId}' cannot be exported as a JSON Web Key."
            );

        return new DPoPProofKey(ProofService, NonceCache, credentials, thumbprint);
    }

    private string? ComputeThumbprint(SecretKey key) =>
        SecretKeyTypeCatalog.TryGet(key.KeyType, out var descriptor)
        && descriptor.ExportPublicKey(key) is { } exported
            ? exported.Jwk.ComputeThumbprint()
            : null;

    private static List<string> BuildPreferredAlgorithms(List<string> supported)
    {
        var result = new List<string>();
        foreach (var algorithm in DefaultPreferredAlgorithms)
        {
            if (supported.Contains(algorithm, StringComparer.Ordinal))
                result.Add(algorithm);
        }

        foreach (var algorithm in supported)
        {
            if (
                !string.Equals(algorithm, NoneAlgorithm, StringComparison.Ordinal)
                && !result.Contains(algorithm, StringComparer.Ordinal)
            )
                result.Add(algorithm);
        }

        return result;
    }
}
