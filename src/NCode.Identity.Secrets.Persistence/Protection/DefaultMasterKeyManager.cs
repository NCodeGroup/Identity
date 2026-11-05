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
/// A rotation creates the successor's key material through the root key provider before anything is written, so a
/// failing root key leaves the owner untouched. The retire and the insert are two units of work, because one save would
/// depend on the provider writing the update before the insert under the unique index of the active keys. A failed
/// insert re-activates the retired row, and a successor that another replica stored first is adopted.
/// </remarks>
// See ADR-1407.
internal sealed class DefaultMasterKeyManager(
    ISecretProtectorProvider protectorProvider,
    IStoreManagerFactory storeManagerFactory,
    IRecoveryKeyWrapper recovery,
    MasterKeyFactory factory,
    IEnumerable<IProtectedValueSource> sources,
    IEnumerable<IMasterKeyChangeObserver> observers,
    IOptions<SecretProtectionOptions> options,
    TimeProvider timeProvider,
    ILogger<DefaultMasterKeyManager> logger
) : IMasterKeyManager
{
    // An observer runs on its own token so that the caller's cancellation cannot leave a committed change unannounced.
    private static readonly TimeSpan ObserverTimeout = TimeSpan.FromSeconds(30);

    // The insert of a successor, and its repair on a token of its own, cannot be cut short by the caller or by a
    // timed-out insert, so neither leaves the owner without an active key. See ADR-1351.
    private static readonly TimeSpan CommitTimeout = TimeSpan.FromSeconds(30);

    private ISecretProtectorProvider ProtectorProvider { get; } = protectorProvider;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private IRecoveryKeyWrapper Recovery { get; } = recovery;

    private MasterKeyFactory Factory { get; } = factory;

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

        var rows = await ListAsync(owner, cancellationToken);
        var active = rows.SingleOrDefault(row => row.RetiredWhen is null);
        var nextVersion = rows.Count == 0 ? 1 : rows.Max(row => row.Version) + 1;

        // A rotation before every replica has loaded the active key protects nothing and only adds a retired key to
        // rewrap and check.
        var spacing = 2 * Options.Value.MasterKeys.RefreshInterval;
        var now = TimeProvider.GetUtcNow();
        var lastRotation = rows.Max(row => row.RetiredWhen);
        if (lastRotation is { } last && last + spacing > now)
            throw new MasterKeyRotationTooSoonException(owner, last + spacing - now);

        if (active is null && rows.Count > 0)
        {
            throw new StoreConcurrencyException(
                "The owner has retired master keys and no active one: another rotation is storing its successor; its next protector creates the key if none follows."
            );
        }

        // The root key provider is called before anything is written, so its failure leaves the owner untouched.
        var created = await Factory.CreateAsync(key, nextVersion, cancellationToken);
        try
        {
            var successor = created.Row;

            if (active is not null)
            {
                await using var retireManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                active.RetiredWhen = now;
                await retireManager
                    .GetStore<IMasterKeyStore>()
                    .UpdateAsync(active, cancellationToken);
                await retireManager.SaveChangesAsync(cancellationToken);
            }

            // From here a change is committed.
            using var commit = new CancellationTokenSource(CommitTimeout, TimeProvider);
            try
            {
                await using var insertManager = await StoreManagerFactory.CreateAsync(commit.Token);
                await insertManager.GetStore<IMasterKeyStore>().AddAsync(successor, commit.Token);
                await insertManager.SaveChangesAsync(commit.Token);
            }
            catch (Exception exception)
            {
                // The repair gets a bounded token of its own: the insert may have failed because its token timed out.
                using var repair = new CancellationTokenSource(CommitTimeout, TimeProvider);
                var winner = await FindActiveAsync(key, repair.Token);
                if (winner is not null && active is not null)
                {
                    Logger.MasterKeyRotationAdoptedSuccessor(owner, winner.KeyId);
                    successor = winner;
                }
                else if (winner is not null)
                {
                    throw new StoreConcurrencyException(
                        "Another writer created the owner's first master key first.",
                        exception
                    );
                }
                else
                {
                    if (active is not null)
                        await RevertRetirementAsync(owner, active, exception, repair.Token);

                    throw;
                }
            }

            ProtectorProvider.Evict(owner);

            var rotation = new MasterKeyRotation(
                owner,
                MasterKeyInfo.From(successor),
                active is null ? null : MasterKeyInfo.From(active)
            );

            Logger.MasterKeyRotated(owner, successor.KeyId, successor.Version);
            await NotifyAsync(owner, (observer, token) => observer.RotatedAsync(rotation, token));

            return rotation;
        }
        finally
        {
            // The ring unwraps the stored row on its next load.
            created.Key.Dispose();
        }
    }

    // Re-activates the retired row after the successor could not be stored. When this fails too, the next protector of
    // the owner creates its key.
    private async ValueTask RevertRetirementAsync(
        SecretOwner owner,
        PersistedMasterKey retired,
        Exception cause,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<IMasterKeyStore>();
            var current = (
                await store.FindByKeyIdsAsync(owner.Normalize(), [retired.KeyId], cancellationToken)
            ).SingleOrDefault();
            if (current is null)
                return;

            current.RetiredWhen = null;
            await store.UpdateAsync(current, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);

            Logger.MasterKeyRotationReverted(cause, owner, current.KeyId);
        }
        catch (Exception revertException)
        {
            Logger.MasterKeyRotationRevertFailed(revertException, owner);
        }
    }

    private async ValueTask<PersistedMasterKey?> FindActiveAsync(
        SecretOwner key,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        return await storeManager
            .GetStore<IMasterKeyStore>()
            .FindActiveAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<ProtectedValueRewrapResult> RewrapAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        // The keys are read again so that a protector obtained afterwards holds the retired keys of this owner.
        ProtectorProvider.Evict(owner);

        var rows = await ListAsync(owner, cancellationToken);

        var retired = rows.Where(row => row.RetiredWhen is not null)
            .Select(row => row.KeyId)
            .ToHashSet();
        if (retired.Count == 0)
        {
            Logger.MasterKeysRewrapped(owner, 0, 0);
            return ProtectedValueRewrapResult.None;
        }

        var total = ProtectedValueRewrapResult.None;
        foreach (var source in Sources)
        {
            var result = await source.RewrapAsync(owner, retired, cancellationToken);
            total = new ProtectedValueRewrapResult(
                total.Rewrapped + result.Rewrapped,
                total.Failed + result.Failed
            );
        }

        Logger.MasterKeysRewrapped(owner, total.Rewrapped, total.Failed);
        if (total.Failed > 0)
            Logger.MasterKeyRewrapIncomplete(owner, total.Failed);

        return total;
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

        var candidates = rows.Where(row =>
                row.RetiredWhen is { } retiredWhen && retiredWhen <= cutoff
            )
            .ToList();
        if (candidates.Count == 0)
            return [];

        // Each source is asked once for every candidate.
        var candidateIds = candidates.Select(row => row.KeyId).ToHashSet();
        var referenced = new HashSet<Guid>();
        foreach (var source in Sources)
        {
            referenced.UnionWith(
                await source.FindReferencedAsync(owner, candidateIds, cancellationToken)
            );
        }

        var destroyed = new List<MasterKeyInfo>();
        try
        {
            foreach (var row in candidates)
            {
                if (referenced.Contains(row.KeyId))
                    continue;

                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var removed = await storeManager
                    .GetStore<IMasterKeyStore>()
                    .RemoveAsync(row, cancellationToken);
                if (!removed)
                    continue;

                await storeManager.SaveChangesAsync(cancellationToken);

                Logger.MasterKeyDestroyed(owner, row.KeyId, row.Version);
                destroyed.Add(MasterKeyInfo.From(row));
            }
        }
        finally
        {
            // The keys already removed are committed, so they are announced even when a later removal fails or the
            // caller cancels.
            await AnnounceDestroyedAsync(owner, destroyed);
        }

        return destroyed;
    }

    private async ValueTask AnnounceDestroyedAsync(SecretOwner owner, List<MasterKeyInfo> destroyed)
    {
        if (destroyed.Count == 0)
            return;

        ProtectorProvider.Evict(owner);
        foreach (var info in destroyed)
        {
            await NotifyAsync(owner, (observer, token) => observer.DestroyedAsync(info, token));
        }
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
