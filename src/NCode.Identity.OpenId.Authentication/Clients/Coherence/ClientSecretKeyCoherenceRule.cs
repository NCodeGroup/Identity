// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Clients.Coherence;

/// <summary>
/// Checks that a client registers a symmetric <c>request_object_signing_alg_values_supported</c> value, such as
/// <c>HS256</c>, only when its effective <c>token_endpoint_auth_method</c> uses the <c>client_secret</c> as a key. A
/// request object signed with a symmetric algorithm is keyed from the client's secret (OpenID Connect Core §10.1), and
/// a client of a method that only compares its secret holds its secret as a one-way verifier, which keys nothing. A
/// value the client inherits from its tenant is not the client's registration, so it is not judged.
/// </summary>
// See ADR-1206 and ADR-1391.
internal sealed class ClientSecretKeyCoherenceRule(
    IClientAuthenticationMethodCatalog clientAuthenticationMethodCatalog,
    IAlgorithmCollectionProvider algorithmCollectionProvider
) : IClientCoherenceRule
{
    private static string[] KeySettingNames { get; } =
    [
        OpenIdSettingNames.TokenEndpointAuthMethod,
        OpenIdSettingNames.RequestObjectSigningAlgValuesSupported,
    ];

    private IClientAuthenticationMethodCatalog ClientAuthenticationMethodCatalog { get; } =
        clientAuthenticationMethodCatalog;

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    /// <summary>
    /// Gets the names of the setting that registers the client-authentication method and of the request-object signing
    /// algorithms.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames => KeySettingNames;

    /// <summary>
    /// Finds every symmetric request-object signing algorithm a client registers when its effective method keys
    /// nothing from its secret.
    /// </summary>
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violations, or an empty list when the client's secret can key every algorithm it registers.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context)
    {
        if (
            !context.RegisteredSettings.TryGetValue(
                OpenIdSettingKeys.RequestObjectSigningAlgValuesSupported,
                out var algorithms
            )
        )
        {
            return [];
        }

        var method = context.EffectiveSettings.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod);
        if (
            ClientAuthenticationMethodCatalog.TryGet(method, out var descriptor)
            && descriptor.UsesClientSecretAsKey
        )
        {
            return [];
        }

        var collection = AlgorithmCollectionProvider.Collection;
        List<ClientCoherenceViolation> violations = [];
        foreach (var algorithm in algorithms)
        {
            if (
                collection.TryGetSignatureAlgorithm(algorithm, out var signatureAlgorithm)
                && signatureAlgorithm.KeyTypes.Any(keyType =>
                    typeof(SymmetricSecretKey).IsAssignableFrom(keyType.KeyClrType)
                )
            )
            {
                violations.Add(
                    new ClientCoherenceViolation(
                        OpenIdSettingNames.RequestObjectSigningAlgValuesSupported,
                        algorithm,
                        OpenIdSettingNames.TokenEndpointAuthMethod
                    )
                    {
                        Description =
                            $"The '{OpenIdSettingNames.RequestObjectSigningAlgValuesSupported}' value '{algorithm}' "
                            + "is keyed from the client secret, which the effective "
                            + $"'{OpenIdSettingNames.TokenEndpointAuthMethod}' value '{method}' stores as a one-way "
                            + "verifier.",
                    }
                );
            }
        }

        return violations;
    }
}
