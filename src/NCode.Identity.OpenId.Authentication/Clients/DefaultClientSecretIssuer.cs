// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientSecretIssuer"/>: the registered method's descriptor decides
/// whether a client is issued a secret and in which form, and <see cref="ISecretGenerator"/> stores it.
/// </summary>
// See ADR-1391.
internal sealed class DefaultClientSecretIssuer(
    IClientAuthenticationMethodCatalog clientAuthenticationMethodCatalog,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    ISecretGenerator secretGenerator,
    ICryptoService cryptoService,
    TimeProvider timeProvider
) : IClientSecretIssuer
{
    private IClientAuthenticationMethodCatalog ClientAuthenticationMethodCatalog { get; } =
        clientAuthenticationMethodCatalog;

    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;

    private ISecretGenerator SecretGenerator { get; } = secretGenerator;

    private ICryptoService CryptoService { get; } = cryptoService;

    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public async ValueTask<string?> GetTokenEndpointAuthMethodAsync(
        string teamId,
        string tenantId,
        JsonElement? tenantSettings,
        string clientId,
        JsonElement clientSettings,
        CancellationToken cancellationToken
    )
    {
        if (tenantSettings is not { } tenantDocument)
        {
            return null;
        }

        try
        {
            var teamEffective = await EffectiveSettingsResolver.ResolveTeamAsync(
                teamId,
                cancellationToken
            );
            var tenantEffective = EffectiveSettingsResolver.ResolveStored(
                teamEffective,
                OpenIdSettingScopes.Tenant,
                tenantId,
                tenantDocument
            );
            var clientEffective = EffectiveSettingsResolver.ResolveStored(
                tenantEffective,
                OpenIdSettingScopes.Client,
                clientId,
                clientSettings
            );

            return clientEffective.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod);
        }
        catch (UnreadableSettingsException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool RequiresClientSecret(string tokenEndpointAuthMethod) =>
        ClientAuthenticationMethodCatalog.TryGet(tokenEndpointAuthMethod, out var descriptor)
        && descriptor.RequiresClientSecret;

    /// <inheritdoc />
    public GeneratedSecret GenerateClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        SharedSecretRequest request
    ) =>
        UsesClientSecretAsKey(tokenEndpointAuthMethod)
            ? SecretGenerator.GenerateReversibleSharedSecret(protector, request)
            : SecretGenerator.GenerateSharedSecretVerifier(protector, request);

    /// <inheritdoc />
    public PersistedSecret ImportClientSecret(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        SharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    ) =>
        UsesClientSecretAsKey(tokenEndpointAuthMethod)
            ? SecretGenerator.ImportReversibleSharedSecret(protector, request, secretValue)
            : SecretGenerator.ImportSharedSecretVerifier(protector, request, secretValue);

    /// <inheritdoc />
    public ClientSecretReconciliation ReconcileClientSecrets(
        ISecretProtector protector,
        string? tokenEndpointAuthMethod,
        IReadOnlyCollection<PersistedSecret> secrets
    )
    {
        // A conversion to a verifier cannot be undone, so nothing is rewritten for a method that cannot be resolved.
        // See ADR-1391.
        if (tokenEndpointAuthMethod is null)
        {
            return Unchanged(secrets);
        }

        var usesClientSecretAsKey = UsesClientSecretAsKey(tokenEndpointAuthMethod);
        var now = TimeProvider.GetUtcNow();

        var canServe = secrets.Any(secret =>
            IsSharedSecret(secret)
            && secret.ExpiresWhen > now
            && (!usesClientSecretAsKey || IsReversible(secret))
        );

        return RequiresClientSecret(tokenEndpointAuthMethod) && !canServe
                ? ReplaceSharedSecrets(protector, tokenEndpointAuthMethod, secrets, now)
            : usesClientSecretAsKey ? Unchanged(secrets)
            : ConvertReversibleSecrets(protector, secrets);
    }

    // A secret that serves no method that keys from it is never kept where the server can read it back. See ADR-1391.
    private ClientSecretReconciliation ConvertReversibleSecrets(
        ISecretProtector protector,
        IReadOnlyCollection<PersistedSecret> secrets
    )
    {
        var kept = new List<PersistedSecret>(secrets.Count);
        var removed = new List<PersistedSecret>();
        var added = new List<PersistedSecret>();
        foreach (var secret in secrets)
        {
            if (!IsSharedSecret(secret) || !IsReversible(secret))
            {
                kept.Add(secret);
                continue;
            }

            var verifier = SecretGenerator.ConvertToSharedSecretVerifier(
                protector,
                secret,
                CryptoService.GenerateResourceId()
            );
            removed.Add(secret);
            added.Add(verifier);
            kept.Add(verifier);
        }

        return new ClientSecretReconciliation
        {
            Secrets = kept,
            RemovedSecrets = removed,
            AddedSecrets = added,
        };
    }

    // None of the client's shared secrets can authenticate it with its method any longer, because a client
    // authenticates only with the method it is registered for, so a new secret takes their place. See ADR-1391.
    private ClientSecretReconciliation ReplaceSharedSecrets(
        ISecretProtector protector,
        string tokenEndpointAuthMethod,
        IReadOnlyCollection<PersistedSecret> secrets,
        DateTimeOffset now
    )
    {
        var issued = GenerateClientSecret(
            protector,
            tokenEndpointAuthMethod,
            new SharedSecretRequest
            {
                SecretId = CryptoService.GenerateResourceId(),
                CreatedWhen = now,
                ExpiresWhen = DateTimeOffset.MaxValue,
            }
        );

        var kept = new List<PersistedSecret>(secrets.Count + 1);
        var removed = new List<PersistedSecret>();
        foreach (var secret in secrets)
        {
            if (IsSharedSecret(secret))
            {
                removed.Add(secret);
            }
            else
            {
                kept.Add(secret);
            }
        }

        kept.Add(issued.Secret);

        return new ClientSecretReconciliation
        {
            Secrets = kept,
            RemovedSecrets = removed,
            AddedSecrets = [issued.Secret],
            IssuedSecret = issued,
        };
    }

    private static ClientSecretReconciliation Unchanged(
        IReadOnlyCollection<PersistedSecret> secrets
    ) =>
        new()
        {
            Secrets = [.. secrets],
            RemovedSecrets = [],
            AddedSecrets = [],
        };

    // A client's symmetric secret is its client_secret.
    private static bool IsSharedSecret(PersistedSecret secret) =>
        string.Equals(secret.SecretType, SecretTypes.Symmetric, StringComparison.Ordinal);

    private static bool IsReversible(PersistedSecret secret) =>
        string.Equals(
            secret.SecretEncoding,
            SecretEncodings.SymmetricRaw,
            StringComparison.Ordinal
        );

    // A method that cannot be resolved or is not registered keys nothing from the secret, so its secret is stored as a
    // one-way verifier, which fails closed: it authenticates no method that needs the secret back.
    private bool UsesClientSecretAsKey(string? tokenEndpointAuthMethod) =>
        tokenEndpointAuthMethod is not null
        && ClientAuthenticationMethodCatalog.TryGet(tokenEndpointAuthMethod, out var descriptor)
        && descriptor.UsesClientSecretAsKey;
}
