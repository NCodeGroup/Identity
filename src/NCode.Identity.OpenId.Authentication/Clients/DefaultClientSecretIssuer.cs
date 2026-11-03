// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientSecretIssuer"/>: the registered method's
/// <see cref="ClientAuthenticationMethodDescriptor.UsesClientSecretAsKey"/> picks between the
/// <see cref="ISecretGenerator"/> methods that store a reversible secret and those that store a one-way verifier.
/// </summary>
// See ADR-1110 and ADR-1391.
internal sealed class DefaultClientSecretIssuer(
    IClientAuthenticationMethodCatalog clientAuthenticationMethodCatalog,
    ISecretGenerator secretGenerator
) : IClientSecretIssuer
{
    private IClientAuthenticationMethodCatalog ClientAuthenticationMethodCatalog { get; } =
        clientAuthenticationMethodCatalog;

    private ISecretGenerator SecretGenerator { get; } = secretGenerator;

    /// <inheritdoc />
    public GeneratedSecret GenerateClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        GenerateSecretRequest request
    ) =>
        UsesClientSecretAsKey(tokenEndpointAuthMethod)
            ? SecretGenerator.GenerateReversibleSharedSecret(protector, request)
            : SecretGenerator.GenerateOneWaySharedSecret(protector, request);

    /// <inheritdoc />
    public PersistedSecret ImportClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    ) =>
        UsesClientSecretAsKey(tokenEndpointAuthMethod)
            ? SecretGenerator.ImportReversibleSharedSecret(protector, request, secretValue)
            : SecretGenerator.ImportOneWaySharedSecret(protector, request, secretValue);

    /// <inheritdoc />
    public bool CanAuthenticate(PersistedSecret secret, string? tokenEndpointAuthMethod) =>
        string.Equals(secret.SecretType, SecretTypes.Symmetric, StringComparison.Ordinal)
        && (
            !UsesClientSecretAsKey(tokenEndpointAuthMethod)
            || string.Equals(
                secret.SecretEncoding,
                SecretEncodings.SymmetricRaw,
                StringComparison.Ordinal
            )
        );

    // An unknown or unregistered method keys nothing from the secret, so its secret is stored as a one-way verifier,
    // which fails closed: it authenticates no method that needs the secret back.
    private bool UsesClientSecretAsKey(string? tokenEndpointAuthMethod) =>
        tokenEndpointAuthMethod is not null
        && ClientAuthenticationMethodCatalog.TryGet(tokenEndpointAuthMethod, out var descriptor)
        && descriptor.UsesClientSecretAsKey;
}
