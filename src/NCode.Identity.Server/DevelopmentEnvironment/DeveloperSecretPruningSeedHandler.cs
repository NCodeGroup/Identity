// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Mediator;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// A <strong>development-only</strong> seed handler that removes the secrets of each tenant that can no longer be
/// unprotected, so a developer whose master keys were purged or replaced, or whose local root key changed (for example,
/// after switching machines), keeps a runnable server: the tenant's key provisioning then generates fresh keys in
/// place of the removed ones, and the read path never meets an undecryptable secret.
/// </summary>
/// <remarks>
/// It runs before every other seed contributor, so they all observe only readable secrets. When the master keys of a
/// tenant cannot be unwrapped, it also removes them, so that the tenant starts over with a new master key. Production
/// never registers it: there, an undecryptable key signals a lost or changed root key, and the tenant fails until the
/// root key is restored rather than having its keys silently discarded and replaced.
/// </remarks>
internal sealed class DeveloperSecretPruningSeedHandler(
    ISecretSerializer secretSerializer,
    ISecretProtectorProvider secretProtectorProvider,
    DeveloperMasterKeyReset masterKeyReset
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;

    private DeveloperMasterKeyReset MasterKeyReset { get; } = masterKeyReset;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        SeedTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var context = command.Context;
        var tenant = context.Tenant;

        var unusableSecretIds = await FindUnusableSecretIdsAsync(tenant, cancellationToken);
        if (unusableSecretIds.Count == 0)
        {
            return;
        }

        var store = context.StoreManager.GetStore<ITenantStore>();
        foreach (var secretId in unusableSecretIds)
        {
            await store.RemoveSecretAsync(tenant.TenantId, secretId, cancellationToken);
        }

        // Refresh the in-memory secrets so the contributors that follow, and the materialization after the seed, observe
        // the pruned state without re-reading the still-uncommitted unit of work.
        tenant.Secrets = new PersistedTenantSecrets
        {
            TenantId = tenant.Secrets.TenantId,
            ConcurrencyToken = tenant.Secrets.ConcurrencyToken,
            Value =
            [
                .. tenant.Secrets.Value.Where(secret =>
                    !unusableSecretIds.Contains(secret.SecretId)
                ),
            ],
        };
    }

    // Deserializes each persisted secret individually so a single undecryptable secret (e.g. protected by a
    // purged or replaced master key) can be identified for pruning instead of failing the whole batch. When the master
    // keys of the tenant cannot be unwrapped (e.g. the local root key changed), or an envelope names a master key that
    // no longer exists, no private secret is readable: the master keys are removed so the tenant starts over, and every
    // private secret is unusable.
    private async ValueTask<HashSet<string>> FindUnusableSecretIdsAsync(
        PersistedTenant tenant,
        CancellationToken cancellationToken
    )
    {
        var unusableSecretIds = new HashSet<string>(StringComparer.Ordinal);

        var owner = SecretOwner.ForTenant(tenant.TenantId);

        ISecretProtector protector;
        try
        {
            protector = await SecretProtectorProvider.GetProtectorForSecretsAsync(
                owner,
                tenant.Secrets.Value,
                cancellationToken
            );
        }
        catch (CryptographicException)
        {
            await MasterKeyReset.ResetAsync(owner, cancellationToken);

            foreach (var persistedSecret in tenant.Secrets.Value)
            {
                if (!SecretEncodings.IsPublic(persistedSecret.SecretEncoding))
                {
                    unusableSecretIds.Add(persistedSecret.SecretId);
                }
            }

            return unusableSecretIds;
        }

        foreach (var persistedSecret in tenant.Secrets.Value)
        {
            SecretKey? secretKey = null;
            try
            {
                secretKey = SecretSerializer.DeserializeSecret(protector, persistedSecret);
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

        return unusableSecretIds;
    }
}
