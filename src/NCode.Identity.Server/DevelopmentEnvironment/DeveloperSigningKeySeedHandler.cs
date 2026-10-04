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

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Mediator;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// A <strong>development-only</strong> seed handler that ensures each tenant has a persisted <c>RSA</c> signing key,
/// generating and storing one through the normal persistence path when absent, so the runtime, the JWKS endpoint, and
/// the management API all read a single source of truth. It runs on the tenant seed path, which executes on the same
/// cache-miss materialization boundary the key is needed for.
/// </summary>
/// <remarks>
/// The seed is self-healing: secrets that cannot be unprotected (for example, because the data-protection key ring was
/// deleted) are pruned, and a fresh key is generated when none of the surviving secrets are usable, so the server stays
/// runnable. This is intended for local development only and must never be used in production, where a stable,
/// securely-managed signing key with a deliberate rotation policy is required.
/// </remarks>
internal sealed class DeveloperSigningKeySeedHandler(
    ISecretSerializer secretSerializer,
    ISecretGenerator secretGenerator,
    ICryptoService cryptoService,
    TimeProvider timeProvider
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    private const int RsaKeySizeBits = 2048;

    private ISecretSerializer SecretSerializer { get; } = secretSerializer;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private ICryptoService CryptoService { get; } = cryptoService;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        SeedTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var context = command.Context;
        var tenant = context.Tenant;

        var (hasUsableSecret, unusableSecretIds) = InspectSecrets(tenant.Secrets);

        // Nothing to heal: at least one usable key and no undecryptable secrets left to trip the read path.
        if (hasUsableSecret && unusableSecretIds.Count == 0)
        {
            return;
        }

        var store = context.StoreManager.GetStore<ITenantStore>();

        // Prune secrets that cannot be unprotected (e.g. the data-protection key ring changed when switching
        // machines) so the read path does not fail deserializing them.
        foreach (var secretId in unusableSecretIds)
        {
            await store.RemoveSecretAsync(tenant.TenantId, secretId, cancellationToken);
        }

        PersistedSecret? generatedSecret = null;
        if (!hasUsableSecret)
        {
            generatedSecret = SecretGenerator.GenerateSecret(
                new GenerateSecretRequest
                {
                    SecretId = CryptoService.GenerateResourceId(),
                    SecretType = SecretTypes.Rsa,
                    KeySizeBits = RsaKeySizeBits,
                    Use = SecretKeyUses.Signature,
                    Algorithm = AlgorithmCodes.DigitalSignature.RsaSha256,
                    CreatedWhen = TimeProvider.GetUtcNow(),
                    ExpiresWhen = TimeProvider.GetUtcNow().AddYears(100),
                }
            );

            await store.AddSecretAsync(tenant.TenantId, generatedSecret, cancellationToken);
        }

        // Refresh the in-memory secrets so the materialization that follows this seed observes the pruned and/or
        // seeded state without re-reading the still-uncommitted unit of work.
        tenant.Secrets = RebuildSecrets(tenant.Secrets, unusableSecretIds, generatedSecret);
    }

    private static PersistedTenantSecrets RebuildSecrets(
        PersistedTenantSecrets current,
        IReadOnlyCollection<string> prunedSecretIds,
        PersistedSecret? generatedSecret
    )
    {
        var surviving = current
            .Value.Where(secret => !prunedSecretIds.Contains(secret.SecretId))
            .ToList();

        if (generatedSecret is not null)
        {
            surviving.Add(generatedSecret);
        }

        return new PersistedTenantSecrets
        {
            TenantId = current.TenantId,
            ConcurrencyToken = current.ConcurrencyToken,
            Value = surviving,
        };
    }

    // Deserializes each persisted secret individually so a single undecryptable secret (e.g. protected by a
    // now-unavailable data-protection key) can be identified for pruning instead of failing the whole batch.
    private (bool HasUsableSecret, IReadOnlyCollection<string> UnusableSecretIds) InspectSecrets(
        PersistedTenantSecrets secrets
    )
    {
        var hasUsableSecret = false;
        var unusableSecretIds = new List<string>();

        foreach (var persistedSecret in secrets.Value)
        {
            SecretKey? secretKey = null;
            try
            {
                secretKey = SecretSerializer.DeserializeSecret(persistedSecret);
                hasUsableSecret = true;
            }
            catch (Exception exception)
                when (exception
                        is CryptographicException
                            or FormatException
                            or InvalidOperationException
                )
            {
                unusableSecretIds.Add(persistedSecret.SecretId);
            }
            finally
            {
                if (secretKey is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        return (hasUsableSecret, unusableSecretIds);
    }
}
