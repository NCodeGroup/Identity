// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// A <strong>development-only</strong> reset that removes every master key of an owner, so that the next protector of
/// the owner starts it over with a new master key.
/// </summary>
internal sealed class DeveloperMasterKeyReset(
    IStoreManagerFactory storeManagerFactory,
    ISecretProtectorProvider secretProtectorProvider
)
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;

    /// <summary>
    /// Removes every master key of the specified <paramref name="owner"/> and evicts the owner from the key ring.
    /// </summary>
    public async ValueTask ResetAsync(SecretOwner owner, CancellationToken cancellationToken)
    {
        // A separate, committed unit of work: the key ring reloads through its own store manager, which would not
        // see a deletion that is still uncommitted in another unit of work.
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        await storeManager.GetStore<IMasterKeyStore>().RemoveAllAsync(owner, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        SecretProtectorProvider.Evict(owner);
    }
}
