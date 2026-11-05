// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logging;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the default implementation of <see cref="IMasterKeyRewrapper"/>.
/// </summary>
/// <remarks>
/// Each row is rewrapped in its own unit of work. A row that vanished during the run, whether its update conflicts or
/// finds it gone, counts as examined only. The material of a master key does not change, so no replica needs to
/// reload it. The target of a row is the root of its owner: the root key the owner brings when it has one that is not
/// being removed, otherwise the configured root key provider. A row that already sits under the owner root key is
/// examined only, and a row that is moved under it loses its recovery copy; a row moved under the deployment's root
/// key gets a recovery copy when a recovery public key is configured, and when none is, the existing recovery copy is
/// left as it is, because a recovery path is never removed silently.
/// </remarks>
// See ADR-1408 and ADR-1411.
internal sealed class DefaultMasterKeyRewrapper(
    MasterKeyFactory factory,
    IStoreManagerFactory storeManagerFactory,
    IRecoveryKeyWrapper recovery,
    ILogger<DefaultMasterKeyRewrapper> logger
) : IMasterKeyRewrapper
{
    private const int PageSize = 100;

    private const int MaxAttempts = 3;

    private MasterKeyFactory Factory { get; } = factory;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private IRecoveryKeyWrapper Recovery { get; } = recovery;

    private ILogger<DefaultMasterKeyRewrapper> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<MasterKeyRewrapResult> RewrapAsync(
        MasterKeyRewrapRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RecoveryOnly && !Recovery.IsConfigured)
        {
            throw new InvalidOperationException(
                "A recovery-only rewrap needs a recovery public key; configure SecretProtection:Recovery:PublicKeyPath."
            );
        }

        using var unwrapper = request.RecoveryPrivateKeyPem.IsEmpty
            ? null
            : RecoveryKeyUnwrapper.FromPem(request.RecoveryPrivateKeyPem.Span);

        var total = new MasterKeyRewrapResult(0, 0, 0, 0);
        var rootKeys = new Dictionary<SecretOwner, PersistedOwnerRootKey?>(
            SecretOwnerComparer.Instance
        );

        if (request.Owner is { } owner)
        {
            var rows = await ListAsync(owner, rootKeys, cancellationToken);
            foreach (var row in rows)
            {
                total = Add(
                    total,
                    await ProcessAsync(row, request, unwrapper, rootKeys, cancellationToken)
                );
            }
        }
        else
        {
            string? cursor = null;
            do
            {
                var page = await GetPageAsync(cursor, rootKeys, cancellationToken);
                foreach (var row in page.Items)
                {
                    total = Add(
                        total,
                        await ProcessAsync(row, request, unwrapper, rootKeys, cancellationToken)
                    );
                }

                cursor = page.NextCursor;
            } while (cursor is not null);
        }

        Logger.MasterKeyRewrapCompleted(
            total.Examined,
            total.Rewrapped,
            total.Verified,
            total.Failed
        );

        return total;
    }

    private static MasterKeyRewrapResult Add(
        MasterKeyRewrapResult total,
        MasterKeyRewrapResult row
    ) =>
        new(
            total.Examined + row.Examined,
            total.Rewrapped + row.Rewrapped,
            total.Verified + row.Verified,
            total.Failed + row.Failed
        );

    // Lists the rows of an owner and reads the owner's root key through the same unit of work.
    private async ValueTask<IReadOnlyList<PersistedMasterKey>> ListAsync(
        SecretOwner owner,
        Dictionary<SecretOwner, PersistedOwnerRootKey?> rootKeys,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var rows = await storeManager
            .GetStore<IMasterKeyStore>()
            .ListAsync(owner.Normalize(), cancellationToken);

        await ReadRootKeysAsync(storeManager, rows, rootKeys, cancellationToken);
        return rows;
    }

    // Reads a page of rows and the root key of each owner on it that this run has not read yet through the same unit of
    // work.
    private async ValueTask<PagedResult<PersistedMasterKey>> GetPageAsync(
        string? cursor,
        Dictionary<SecretOwner, PersistedOwnerRootKey?> rootKeys,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var page = await storeManager
            .GetStore<IMasterKeyStore>()
            .GetPageAsync(new PageRequest { Limit = PageSize, Cursor = cursor }, cancellationToken);

        await ReadRootKeysAsync(storeManager, page.Items, rootKeys, cancellationToken);
        return page;
    }

    private static async ValueTask ReadRootKeysAsync(
        IStoreManager storeManager,
        IEnumerable<PersistedMasterKey> rows,
        Dictionary<SecretOwner, PersistedOwnerRootKey?> rootKeys,
        CancellationToken cancellationToken
    )
    {
        foreach (var owner in rows.Select(row => row.Owner).Distinct(SecretOwnerComparer.Instance))
        {
            if (rootKeys.ContainsKey(owner))
                continue;

            rootKeys[owner] = await storeManager
                .GetStore<IOwnerRootKeyStore>()
                .FindAsync(owner.Normalize(), cancellationToken);
        }
    }

    // Rewraps one row in its own unit of work, retrying when another writer changed the row, and returns the outcome.
    private async ValueTask<MasterKeyRewrapResult> ProcessAsync(
        PersistedMasterKey row,
        MasterKeyRewrapRequest request,
        RecoveryKeyUnwrapper? unwrapper,
        Dictionary<SecretOwner, PersistedOwnerRootKey?> rootKeys,
        CancellationToken cancellationToken
    )
    {
        try
        {
            rootKeys.TryGetValue(row.Owner, out var rootKey);
            var ownerRooted = MasterKeyFactory.IsOwnerRooted(rootKey);

            for (var attempt = 1; ; attempt++)
            {
                var conforms = MasterKeyFactory.Conforms(row, rootKey);

                // A row that conforms is left as it is: the online conformance reads nothing of it, and a dry run
                // still proves that its owner root key unwraps it.
                if (request.NonconformingOnly && conforms)
                    return new MasterKeyRewrapResult(1, 0, 0, 0);

                if (ownerRooted && conforms)
                {
                    if (!request.DryRun)
                        return new MasterKeyRewrapResult(1, 0, 0, 0);

                    using var verified = await Factory.UnwrapAsync(row, cancellationToken);
                    return new MasterKeyRewrapResult(1, 0, 1, 0);
                }

                // A recovery-only run on an owner root key only removes a recovery copy that a row still carries.
                if (
                    request is { RecoveryOnly: true }
                    && ownerRooted
                    && row.RecoveryWrappedKey is null
                    && row.RecoveryKeyId is null
                )
                {
                    return new MasterKeyRewrapResult(1, 0, 0, 0);
                }

                // A row under an owner root key has no recovery copy, so it unwraps through its own key.
                using var masterKey =
                    unwrapper is null || row.RootKeyReference is not null
                        ? await Factory.UnwrapAsync(row, cancellationToken)
                        : MasterKeyFactory.UnwrapWithRecovery(row, unwrapper);

                if (request.DryRun)
                {
                    return new MasterKeyRewrapResult(1, 0, 1, 0);
                }

                await ApplyWrapsAsync(row, masterKey, rootKey, request, cancellationToken);

                try
                {
                    await SaveAsync(row, cancellationToken);
                    return new MasterKeyRewrapResult(1, 1, 0, 0);
                }
                catch (Exception exception)
                    when (exception is StoreConcurrencyException or InvalidOperationException)
                {
                    // A row the maintenance pass destroyed during the run is gone, and counts as examined only.
                    var current = await FindAsync(row, cancellationToken);
                    if (current is null)
                        return new MasterKeyRewrapResult(1, 0, 0, 0);

                    // Another writer changed the row, so it is read again and rewrapped from its current state.
                    if (exception is not StoreConcurrencyException || attempt >= MaxAttempts)
                        throw;

                    row = current;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.MasterKeyRewrapRowFailed(exception, row.Owner, row.KeyId, row.Version);
            return new MasterKeyRewrapResult(1, 0, 0, 1);
        }
    }

    // Replaces the root wrap, unless only the recovery copy is replaced, and the recovery copy, on the row. Under an
    // owner root key the row loses its recovery copy and gains no other; under the deployment's root key the recovery
    // copy is replaced when a recovery public key is configured.
    private async ValueTask ApplyWrapsAsync(
        PersistedMasterKey row,
        MasterKey masterKey,
        PersistedOwnerRootKey? rootKey,
        MasterKeyRewrapRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request.RecoveryOnly)
        {
            if (MasterKeyFactory.IsOwnerRooted(rootKey))
            {
                row.RecoveryKeyId = null;
                row.RecoveryWrappedKey = null;
            }
            else if (await Factory.WrapUnderRecoveryAsync(masterKey, cancellationToken) is { } only)
            {
                row.RecoveryKeyId = only.KeyId;
                row.RecoveryWrappedKey = only.Ciphertext;
            }

            return;
        }

        var wraps = await Factory.WrapAsync(masterKey, rootKey, cancellationToken);

        row.RootKeyProvider = wraps.Root.WrapperName;
        row.RootKeyId = wraps.Root.KeyId;
        row.RootKeyReference = wraps.RootKeyReference;
        row.RootWrappedKey = wraps.Root.Ciphertext;

        if (wraps.ClearsRecovery)
        {
            row.RecoveryKeyId = null;
            row.RecoveryWrappedKey = null;
        }
        else if (wraps.Recovery is { } recovery)
        {
            row.RecoveryKeyId = recovery.KeyId;
            row.RecoveryWrappedKey = recovery.Ciphertext;
        }
    }

    private async ValueTask SaveAsync(PersistedMasterKey row, CancellationToken cancellationToken)
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        await storeManager.GetStore<IMasterKeyStore>().UpdateAsync(row, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    private async ValueTask<PersistedMasterKey?> FindAsync(
        PersistedMasterKey row,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var rows = await storeManager
            .GetStore<IMasterKeyStore>()
            .FindByKeyIdsAsync(row.Owner.Normalize(), [row.KeyId], cancellationToken);

        return rows.SingleOrDefault();
    }
}
