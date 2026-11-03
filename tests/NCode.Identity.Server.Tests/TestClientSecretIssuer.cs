// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Server.Tests;

/// <summary>
/// Provides an <see cref="IClientSecretIssuer"/> over the real <see cref="ISecretGenerator"/>, whose span-taking import
/// a Moq mock cannot set up: a secret issued for one of the methods the test declares as keying from the secret is
/// stored reversible, and any other as a one-way verifier, as the registered descriptors decide in production. It
/// records the method each secret is issued for, so a test proves which method a seed handed it.
/// </summary>
internal sealed class TestClientSecretIssuer(
    ISecretGenerator secretGenerator,
    params string[] keyedMethods
) : IClientSecretIssuer
{
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;

    private HashSet<string> KeyedMethods { get; } = new(keyedMethods, StringComparer.Ordinal);

    /// <summary>
    /// Gets the method each secret was issued for, in order.
    /// </summary>
    public List<string?> IssuedMethods { get; } = [];

    public GeneratedSecret GenerateClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        GenerateSecretRequest request
    ) => throw new NotSupportedException("A seed imports the secret it is configured with.");

    public PersistedSecret ImportClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    )
    {
        IssuedMethods.Add(tokenEndpointAuthMethod);
        return tokenEndpointAuthMethod is not null && KeyedMethods.Contains(tokenEndpointAuthMethod)
            ? SecretGenerator.ImportReversibleSharedSecret(protector, request, secretValue)
            : SecretGenerator.ImportOneWaySharedSecret(protector, request, secretValue);
    }

    public bool CanAuthenticate(PersistedSecret secret, string? tokenEndpointAuthMethod) =>
        throw new NotSupportedException("A seed never judges a stored secret.");
}
