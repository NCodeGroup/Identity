// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Provides the shared flow of a source whose protected values are rows that a store pages and rewrites: one pass per
/// owner over every requested master key reads only the rows whose value starts with one of the keys' prefixes, picks
/// those whose value names a requested key, rewraps them with one protector per page, and writes the page in its own
/// unit of work. A value that cannot be rewrapped is counted failed and the others of its page are written; a page that
/// another writer changed is read and rewrapped again, a few times at most, and then counted failed.
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
    /// Reads a page of the protected values that an owner holds and that start with one of the prefixes.
    /// </summary>
    protected abstract ValueTask<PagedResult<PersistedProtectedValue>> GetPageAsync(
        IStoreManager storeManager,
        SecretOwner owner,
        IReadOnlyCollection<string> prefixes,
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Writes a rewrapped value.
    /// </summary>
    /// <returns><see langword="true"/> when the value was written, or <see langword="false"/> when its row is gone.</returns>
    protected abstract ValueTask<bool> UpdateAsync(
        IStoreManager storeManager,
        PersistedProtectedValue value,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the protection context that a value was protected under.
    /// </summary>
    protected abstract SecretProtectionContext GetContext(PersistedProtectedValue value);

    /// <inheritdoc />
    public async ValueTask<ProtectedValueRewrapResult> RewrapAsync(
        SecretOwner owner,
        IReadOnlySet<Guid> masterKeyIds,
        CancellationToken cancellationToken
    )
    {
        if (!Handles(owner) || masterKeyIds.Count == 0)
            return ProtectedValueRewrapResult.None;

        var prefixes = GetPrefixes(masterKeyIds);

        var rewrapped = 0;
        var failed = 0;
        string? cursor = null;
        do
        {
            (var pageRewrapped, var pageFailed, cursor) = await RewrapPageAsync(
                owner,
                masterKeyIds,
                prefixes,
                cursor,
                cancellationToken
            );
            rewrapped += pageRewrapped;
            failed += pageFailed;
        } while (cursor is not null);

        return new ProtectedValueRewrapResult(rewrapped, failed);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlySet<Guid>> FindReferencedAsync(
        SecretOwner owner,
        IReadOnlySet<Guid> masterKeyIds,
        CancellationToken cancellationToken
    )
    {
        var found = new HashSet<Guid>();
        if (!Handles(owner) || masterKeyIds.Count == 0)
            return found;

        var prefixes = GetPrefixes(masterKeyIds);

        string? cursor = null;
        do
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var page = await GetPageAsync(
                storeManager,
                owner,
                prefixes,
                new PageRequest { Limit = PageSize, Cursor = cursor },
                cancellationToken
            );

            foreach (var value in page.Items)
            {
                if (
                    ProtectorProvider.TryReadMasterKeyId(value.ProtectedValue, out var keyId)
                    && masterKeyIds.Contains(keyId)
                )
                {
                    found.Add(keyId);
                }
            }

            if (found.Count == masterKeyIds.Count)
                break;

            cursor = page.NextCursor;
        } while (cursor is not null);

        return found;
    }

    private string[] GetPrefixes(IReadOnlySet<Guid> masterKeyIds) =>
        [.. masterKeyIds.Select(ProtectorProvider.GetMasterKeyIdPrefix)];

    // The prefix only narrows the rows, so the key is confirmed from the value itself.
    private bool NamesKey(PersistedProtectedValue value, IReadOnlySet<Guid> masterKeyIds) =>
        ProtectorProvider.TryReadMasterKeyId(value.ProtectedValue, out var keyId)
        && masterKeyIds.Contains(keyId);

    // Rewraps the values of one page that name a key, and returns how many were written, how many failed and the
    // cursor of the next page.
    private async ValueTask<(int Rewrapped, int Failed, string? NextCursor)> RewrapPageAsync(
        SecretOwner owner,
        IReadOnlySet<Guid> masterKeyIds,
        IReadOnlyCollection<string> prefixes,
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
                prefixes,
                new PageRequest { Limit = PageSize, Cursor = cursor },
                cancellationToken
            );

            var matching = page.Items.Where(value => NamesKey(value, masterKeyIds)).ToList();
            if (matching.Count == 0)
                return (0, 0, page.NextCursor);

            var rewrapped = 0;
            var failed = 0;
            try
            {
                // One protector per page, which loads the retired keys that the values name.
                var protector = await ProtectorProvider.GetProtectorAsync(
                    owner,
                    [.. matching.Select(value => value.ProtectedValue)],
                    cancellationToken
                );

                foreach (var value in matching)
                {
                    string rewritten;
                    try
                    {
                        rewritten = protector.RewrapToString(
                            value.ProtectedValue,
                            GetContext(value)
                        );
                    }
                    catch (CryptographicException exception)
                    {
                        // The record id is logged and never the value.
                        failed++;
                        Logger.ProtectedValueRewrapFailed(exception, owner, value.RecordId);
                        continue;
                    }

                    value.ProtectedValue = rewritten;

                    // A row that was deleted since the page was read has nothing left to rewrap.
                    if (await UpdateAsync(storeManager, value, cancellationToken))
                        rewrapped++;
                }

                await storeManager.SaveChangesAsync(cancellationToken);
            }
            catch (StoreConcurrencyException) when (attempt < MaxAttempts)
            {
                Logger.ProtectedValuePageConflict(owner, attempt);
                continue;
            }
            catch (StoreConcurrencyException exception)
            {
                Logger.ProtectedValuePageAbandoned(exception, owner, MaxAttempts);
                return (0, matching.Count, page.NextCursor);
            }
            catch (CryptographicException exception)
            {
                // A master key that a value names is gone, so none of the page can be read.
                Logger.ProtectedValuePageUnreadable(exception, owner);
                return (0, matching.Count, page.NextCursor);
            }

            return (rewrapped, failed, page.NextCursor);
        }
    }
}
