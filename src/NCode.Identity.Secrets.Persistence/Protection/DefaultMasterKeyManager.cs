// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logging;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the default implementation of <see cref="IMasterKeyManager"/>.
/// </summary>
/// <remarks>
/// A rotation retires the active key in one unit of work and creates the successor through the key ring in another,
/// which reuses the ring's handling of two replicas that create a key at the same time. Doing both in one save would
/// depend on the provider writing the update before the insert under the unique index of the active keys.
/// </remarks>
// See ADR-1407.
internal sealed class DefaultMasterKeyManager(
    ISecretProtectorProvider protectorProvider,
    IStoreManagerFactory storeManagerFactory,
    IRecoveryKeyWrapper recovery,
    IEnumerable<IProtectedValueSource> sources,
    IEnumerable<IMasterKeyChangeObserver> observers,
    IOptions<SecretProtectionOptions> options,
    TimeProvider timeProvider,
    ILogger<DefaultMasterKeyManager> logger
) : IMasterKeyManager
{
    // An observer runs on its own token so that the caller's cancellation cannot leave a committed change unannounced.
    private static readonly TimeSpan ObserverTimeout = TimeSpan.FromSeconds(30);

    private ISecretProtectorProvider ProtectorProvider { get; } = protectorProvider;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private IRecoveryKeyWrapper Recovery { get; } = recovery;

    private IReadOnlyList<IProtectedValueSource> Sources { get; } = [.. sources];

    private IReadOnlyList<IMasterKeyChangeObserver> Observers { get; } = [.. observers];

    private IOptions<SecretProtectionOptions> Options { get; } = options;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DefaultMasterKeyManager> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<MasterKeyStatus> GetStatusAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var rows = await ListAsync(owner, cancellationToken);

        return new MasterKeyStatus(
            owner,
            [.. rows.Select(MasterKeyInfo.From)],
            Recovery.IsConfigured
        );
    }

    /// <inheritdoc />
    public async ValueTask<MasterKeyRotation> RotateAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var key = owner.Normalize();

        var retired = await RetireActiveAsync(key, cancellationToken);

        // The successor is created through the ring, which stores the next version or adopts the key that another
        // replica stored first.
        ProtectorProvider.Evict(owner);
        await ProtectorProvider.GetProtectorAsync(owner, [], cancellationToken);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var active =
            await storeManager.GetStore<IMasterKeyStore>().FindActiveAsync(key, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The owner '{owner}' has no active master key after its rotation."
            );

        var rotation = new MasterKeyRotation(
            owner,
            MasterKeyInfo.From(active),
            retired is null ? null : MasterKeyInfo.From(retired)
        );

        Logger.MasterKeyRotated(owner, active.KeyId, active.Version);
        await NotifyAsync(owner, (observer, token) => observer.RotatedAsync(rotation, token));

        return rotation;
    }

    /// <inheritdoc />
    public async ValueTask<int> RewrapAsync(SecretOwner owner, CancellationToken cancellationToken)
    {
        // The keys are read again so that a protector obtained afterwards holds the retired keys of this owner.
        ProtectorProvider.Evict(owner);

        var rows = await ListAsync(owner, cancellationToken);

        var rewrapped = 0;
        foreach (var row in rows.Where(row => row.RetiredWhen is not null))
        {
            foreach (var source in Sources)
            {
                rewrapped += await source.RewrapAsync(owner, row.KeyId, cancellationToken);
            }
        }

        Logger.MasterKeysRewrapped(owner, rewrapped);

        return rewrapped;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<MasterKeyInfo>> DestroyRetiredAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var masterKeys = Options.Value.MasterKeys;
        if (!masterKeys.DestroyRetired)
            return [];

        var cutoff = TimeProvider.GetUtcNow() - masterKeys.RetirementGrace;
        var rows = await ListAsync(owner, cancellationToken);

        var destroyed = new List<MasterKeyInfo>();
        foreach (var row in rows)
        {
            if (row.RetiredWhen is not { } retiredWhen || retiredWhen > cutoff)
                continue;

            if (await IsReferencedAsync(owner, row.KeyId, cancellationToken))
                continue;

            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var removed = await storeManager
                .GetStore<IMasterKeyStore>()
                .RemoveAsync(row, cancellationToken);
            if (!removed)
                continue;

            await storeManager.SaveChangesAsync(cancellationToken);

            Logger.MasterKeyDestroyed(owner, row.KeyId, row.Version);
            destroyed.Add(MasterKeyInfo.From(row));
        }

        if (destroyed.Count == 0)
            return destroyed;

        ProtectorProvider.Evict(owner);
        foreach (var info in destroyed)
        {
            await NotifyAsync(owner, (observer, token) => observer.DestroyedAsync(info, token));
        }

        return destroyed;
    }

    private async ValueTask<IReadOnlyList<PersistedMasterKey>> ListAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        return await storeManager
            .GetStore<IMasterKeyStore>()
            .ListAsync(owner.Normalize(), cancellationToken);
    }

    // Retires the active row in its own unit of work and returns it, or null when the owner has no active row.
    private async ValueTask<PersistedMasterKey?> RetireActiveAsync(
        SecretOwner key,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IMasterKeyStore>();

        var active = await store.FindActiveAsync(key, cancellationToken);
        if (active is null)
            return null;

        active.RetiredWhen = TimeProvider.GetUtcNow();
        await store.UpdateAsync(active, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return active;
    }

    private async ValueTask<bool> IsReferencedAsync(
        SecretOwner owner,
        Guid masterKeyId,
        CancellationToken cancellationToken
    )
    {
        foreach (var source in Sources)
        {
            if (await source.ReferencesAsync(owner, masterKeyId, cancellationToken))
                return true;
        }

        return false;
    }

    // Calls every observer on a bounded token of its own and swallows each failure, since the change is committed.
    private async ValueTask NotifyAsync(
        SecretOwner owner,
        Func<IMasterKeyChangeObserver, CancellationToken, ValueTask> notify
    )
    {
        foreach (var observer in Observers)
        {
            using var timeout = new CancellationTokenSource(ObserverTimeout, TimeProvider);
            try
            {
                await notify(observer, timeout.Token);
            }
            catch (Exception exception)
            {
                Logger.MasterKeyObserverFailed(exception, owner);
            }
        }
    }
}
