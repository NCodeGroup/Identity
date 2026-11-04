// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Provides the shared flow of a source whose protected values are rows that a store pages and rewrites: it reads the
/// rows in pages, picks those whose value names the master key, rewraps them with one protector per page, and writes
/// the page in its own unit of work. A page that another writer changed is read and rewrapped again, a few times at
/// most.
/// </summary>
// See ADR-1407.
internal abstract class PagedProtectedValueSource(
    IStoreManagerFactory storeManagerFactory,
    ISecretProtectorProvider protectorProvider,
    ILogger logger
) : IProtectedValueSource
{
    private const int PageSize = 100;

    private const int MaxAttempts = 3;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private ISecretProtectorProvider ProtectorProvider { get; } = protectorProvider;

    private ILogger Logger { get; } = logger;

    /// <summary>
    /// Determines whether the source holds values for an owner of this kind.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <returns><see langword="true"/> when the source can hold values for the owner.</returns>
    protected abstract bool Handles(SecretOwner owner);

    /// <summary>
    /// Reads a page of the protected values that an owner holds.
    /// </summary>
    protected abstract ValueTask<PagedResult<PersistedProtectedValue>> GetPageAsync(
        IStoreManager storeManager,
        SecretOwner owner,
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Writes a rewrapped value.
    /// </summary>
    protected abstract ValueTask UpdateAsync(
        IStoreManager storeManager,
        PersistedProtectedValue value,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the protection context that a value was protected under.
    /// </summary>
    protected abstract SecretProtectionContext GetContext(PersistedProtectedValue value);

    /// <inheritdoc />
    public async ValueTask<int> RewrapAsync(
        SecretOwner owner,
        Guid masterKeyId,
        CancellationToken cancellationToken
    )
    {
        if (!Handles(owner))
            return 0;

        var rewrapped = 0;
        string? cursor = null;
        do
        {
            (var count, cursor) = await RewrapPageAsync(
                owner,
                masterKeyId,
                cursor,
                cancellationToken
            );
            rewrapped += count;
        } while (cursor is not null);

        return rewrapped;
    }

    /// <inheritdoc />
    public async ValueTask<bool> ReferencesAsync(
        SecretOwner owner,
        Guid masterKeyId,
        CancellationToken cancellationToken
    )
    {
        if (!Handles(owner))
            return false;

        string? cursor = null;
        do
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var page = await GetPageAsync(
                storeManager,
                owner,
                new PageRequest { Limit = PageSize, Cursor = cursor },
                cancellationToken
            );

            if (page.Items.Any(value => NamesKey(value, masterKeyId)))
                return true;

            cursor = page.NextCursor;
        } while (cursor is not null);

        return false;
    }

    private bool NamesKey(PersistedProtectedValue value, Guid masterKeyId) =>
        ProtectorProvider.TryReadMasterKeyId(value.ProtectedValue, out var keyId)
        && keyId == masterKeyId;

    // Rewraps the values of one page that name the key, and returns how many and the cursor of the next page.
    private async ValueTask<(int Count, string? NextCursor)> RewrapPageAsync(
        SecretOwner owner,
        Guid masterKeyId,
        string? cursor,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var page = await GetPageAsync(
                storeManager,
                owner,
                new PageRequest { Limit = PageSize, Cursor = cursor },
                cancellationToken
            );

            var matching = page.Items.Where(value => NamesKey(value, masterKeyId)).ToList();
            if (matching.Count == 0)
                return (0, page.NextCursor);

            try
            {
                // One protector per page, which loads the retired key that the values name.
                var protector = await ProtectorProvider.GetProtectorAsync(
                    owner,
                    [.. matching.Select(value => value.ProtectedValue)],
                    cancellationToken
                );

                foreach (var value in matching)
                {
                    value.ProtectedValue = protector.RewrapToString(
                        value.ProtectedValue,
                        GetContext(value)
                    );
                    await UpdateAsync(storeManager, value, cancellationToken);
                }

                await storeManager.SaveChangesAsync(cancellationToken);
            }
            catch (StoreConcurrencyException) when (attempt < MaxAttempts)
            {
                Logger.ProtectedValuePageConflict(owner, attempt);
                continue;
            }

            return (matching.Count, page.NextCursor);
        }
    }
}
