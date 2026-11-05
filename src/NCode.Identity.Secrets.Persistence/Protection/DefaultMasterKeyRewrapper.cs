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
/// reload it. When a recovery public key is configured, the recovery copy is replaced; when none is, the existing
/// recovery copy is left as it is, because a recovery path is never removed silently.
/// </remarks>
// See ADR-1408.
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

        if (request.Owner is { } owner)
        {
            var rows = await ListAsync(owner, cancellationToken);
            foreach (var row in rows)
            {
                total = Add(total, await ProcessAsync(row, request, unwrapper, cancellationToken));
            }
        }
        else
        {
            string? cursor = null;
            do
            {
                var page = await GetPageAsync(cursor, cancellationToken);
                foreach (var row in page.Items)
                {
                    total = Add(
                        total,
                        await ProcessAsync(row, request, unwrapper, cancellationToken)
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

    private async ValueTask<PagedResult<PersistedMasterKey>> GetPageAsync(
        string? cursor,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        return await storeManager
            .GetStore<IMasterKeyStore>()
            .GetPageAsync(new PageRequest { Limit = PageSize, Cursor = cursor }, cancellationToken);
    }

    // Rewraps one row in its own unit of work, retrying when another writer changed the row, and returns the outcome.
    private async ValueTask<MasterKeyRewrapResult> ProcessAsync(
        PersistedMasterKey row,
        MasterKeyRewrapRequest request,
        RecoveryKeyUnwrapper? unwrapper,
        CancellationToken cancellationToken
    )
    {
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                using var masterKey = unwrapper is null
                    ? await Factory.UnwrapAsync(row, cancellationToken)
                    : MasterKeyFactory.UnwrapWithRecovery(row, unwrapper);

                if (request.DryRun)
                {
                    return new MasterKeyRewrapResult(1, 0, 1, 0);
                }

                await ApplyWrapsAsync(row, masterKey, request, cancellationToken);

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

    // Replaces the root wrap, unless only the recovery copy is replaced, and the recovery copy when a recovery public
    // key is configured, on the row.
    private async ValueTask ApplyWrapsAsync(
        PersistedMasterKey row,
        MasterKey masterKey,
        MasterKeyRewrapRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!request.RecoveryOnly)
        {
            var root = await Factory.WrapUnderRootAsync(masterKey, cancellationToken);
            row.RootKeyProvider = root.WrapperName;
            row.RootKeyId = root.KeyId;
            row.RootWrappedKey = root.Ciphertext;
        }

        if (await Factory.WrapUnderRecoveryAsync(masterKey, cancellationToken) is { } recovery)
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
