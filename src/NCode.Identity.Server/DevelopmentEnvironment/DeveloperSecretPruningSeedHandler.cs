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
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Mediator;
using NCode.Persistence.Stores;

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
    IStoreManagerFactory storeManagerFactory
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

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
            // A separate, committed unit of work: the key ring reloads through its own store manager, which would not
            // see a deletion that is still uncommitted in the unit of work of the seed.
            await using var keyStoreManager = await StoreManagerFactory.CreateAsync(
                cancellationToken
            );
            await keyStoreManager
                .GetStore<IMasterKeyStore>()
                .RemoveAllAsync(owner, cancellationToken);
            await keyStoreManager.SaveChangesAsync(cancellationToken);

            SecretProtectorProvider.Evict(owner);

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
