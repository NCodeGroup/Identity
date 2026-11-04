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
/// Each row is rewrapped in its own unit of work. The material of a master key does not change, so no replica needs to
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

        var counts = new Counts();

        if (request.Owner is { } owner)
        {
            var rows = await ListAsync(owner, cancellationToken);
            foreach (var row in rows)
            {
                await ProcessAsync(row, request, unwrapper, counts, cancellationToken);
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
                    await ProcessAsync(row, request, unwrapper, counts, cancellationToken);
                }

                cursor = page.NextCursor;
            } while (cursor is not null);
        }

        Logger.MasterKeyRewrapCompleted(
            counts.Examined,
            counts.Rewrapped,
            counts.Verified,
            counts.Failed
        );

        return new MasterKeyRewrapResult(
            counts.Examined,
            counts.Rewrapped,
            counts.Verified,
            counts.Failed
        );
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

    // Rewraps one row in its own unit of work, retrying when another writer changed the row, and counts the outcome.
    private async ValueTask ProcessAsync(
        PersistedMasterKey row,
        MasterKeyRewrapRequest request,
        RecoveryKeyUnwrapper? unwrapper,
        Counts counts,
        CancellationToken cancellationToken
    )
    {
        counts.Examined++;

        try
        {
            for (var attempt = 1; ; attempt++)
            {
                using var masterKey = unwrapper is null
                    ? await Factory.UnwrapAsync(row, cancellationToken)
                    : MasterKeyFactory.UnwrapWithRecovery(row, unwrapper);

                if (request.DryRun)
                {
                    counts.Verified++;
                    return;
                }

                await ApplyWrapsAsync(row, masterKey, request, cancellationToken);

                try
                {
                    await SaveAsync(row, cancellationToken);
                    counts.Rewrapped++;
                    return;
                }
                catch (StoreConcurrencyException) when (attempt < MaxAttempts)
                {
                    // Another writer changed the row, so it is read again and rewrapped from its current state.
                    var current = await FindAsync(row, cancellationToken);
                    if (current is null)
                        return;

                    row = current;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            counts.Failed++;
            Logger.MasterKeyRewrapRowFailed(exception, row.Owner, row.KeyId, row.Version);
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

    private sealed class Counts
    {
        public int Examined { get; set; }

        public int Rewrapped { get; set; }

        public int Verified { get; set; }

        public int Failed { get; set; }
    }
}
