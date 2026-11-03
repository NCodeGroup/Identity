// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Server.Tests;

/// <summary>
/// Provides an <see cref="IClientSecretIssuer"/> over the real <see cref="ISecretGenerator"/>, whose span-taking import
/// a Moq mock cannot set up: a secret issued for one of the methods the test declares as keying from the secret is
/// stored reversible, and any other as a one-way verifier, as the registered descriptors decide in production. It
/// resolves every stored client to <see cref="ResolvedMethod"/> and records the method each secret is issued for, so a
/// test proves which method a seed handed it.
/// </summary>
internal sealed class TestClientSecretIssuer(
    ISecretGenerator secretGenerator,
    params string[] keyedMethods
) : IClientSecretIssuer
{
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;

    private HashSet<string> KeyedMethods { get; } = new(keyedMethods, StringComparer.Ordinal);

    /// <summary>
    /// Gets the method a stored client resolves to.
    /// </summary>
    public string ResolvedMethod { get; init; } =
        OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic;

    /// <summary>
    /// Gets the settings document of each client whose method was resolved, in order.
    /// </summary>
    public List<JsonElement> ResolvedClientSettings { get; } = [];

    /// <summary>
    /// Gets the method each secret was issued for, in order.
    /// </summary>
    public List<string?> IssuedMethods { get; } = [];

    public ValueTask<string?> GetTokenEndpointAuthMethodAsync(
        string tenantId,
        JsonElement? tenantSettings,
        string clientId,
        JsonElement clientSettings,
        CancellationToken cancellationToken
    )
    {
        ResolvedClientSettings.Add(clientSettings);
        return ValueTask.FromResult<string?>(ResolvedMethod);
    }

    public bool RequiresClientSecret(string tokenEndpointAuthMethod) =>
        throw new NotSupportedException("A seed stores the secret it is configured with.");

    public GeneratedSecret GenerateClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        SharedSecretRequest request
    ) => throw new NotSupportedException("A seed imports the secret it is configured with.");

    public PersistedSecret ImportClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        SharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    )
    {
        IssuedMethods.Add(tokenEndpointAuthMethod);
        return tokenEndpointAuthMethod is not null && KeyedMethods.Contains(tokenEndpointAuthMethod)
            ? SecretGenerator.ImportReversibleSharedSecret(protector, request, secretValue)
            : SecretGenerator.ImportSharedSecretVerifier(protector, request, secretValue);
    }

    public ClientSecretReconciliation ReconcileClientSecrets(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        IReadOnlyCollection<PersistedSecret> secrets
    ) => throw new NotSupportedException("A seed never changes a client's method.");
}
