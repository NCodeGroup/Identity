// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Reads the singleton <see cref="PersistedInstanceSetup"/> row of an instance and adds it when it is absent.
/// </summary>
internal static class InstanceSetupRecords
{
    /// <summary>
    /// Gets the singleton <see cref="PersistedInstanceSetup"/> row, adding an uninitialized one when it is absent.
    /// The first replica to write the row wins: a replica whose add fails because another replica added the row between
    /// its read and its write returns the row that replica wrote.
    /// </summary>
    /// <param name="storeManagerFactory">The <see cref="IStoreManagerFactory"/> that creates the store managers
    /// the reads and the add run on.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the instance
    /// setup row.</returns>
    public static async ValueTask<PersistedInstanceSetup> GetOrAddAsync(
        IStoreManagerFactory storeManagerFactory,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IInstanceSetupStore>();

        var existing = await store.GetOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return existing;

        var created = new PersistedInstanceSetup
        {
            Key = PersistedInstanceSetup.SingletonKey,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
        };

        await store.AddAsync(created, cancellationToken);

        try
        {
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Another replica may have added the row between the read above and this write — two replicas reaching an
            // uninitialized instance in one rollout, say. The row it wrote is the row; anything else is a genuine
            // failure.
            var concurrent = await FindAsync(storeManagerFactory, cancellationToken);
            if (concurrent is null)
                throw;

            return concurrent;
        }

        return created;
    }

    private static async ValueTask<PersistedInstanceSetup?> FindAsync(
        IStoreManagerFactory storeManagerFactory,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IInstanceSetupStore>();
        return await store.GetOrDefaultAsync(cancellationToken);
    }
}
