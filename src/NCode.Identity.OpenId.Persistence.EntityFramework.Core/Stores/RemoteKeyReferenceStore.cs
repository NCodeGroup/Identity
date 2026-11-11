// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IRemoteKeyReferenceStore"/> that uses Entity Framework Core for
/// persistence. It reads the key references that the remote secrets of every owner hold.
/// </summary>
/// <remarks>
/// This store does not save changes. It reads the secrets of every owner, so its queries are not scoped to a tenant.
/// </remarks>
// See ADR-1478.
internal class RemoteKeyReferenceStore(
    IStoreProvider storeProvider,
    OpenIdDbContext openIdDbContext
) : IRemoteKeyReferenceStore
{
    private IStoreProvider StoreProvider { get; } = storeProvider;

    private OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    public object? GetService(Type serviceType) => StoreProvider.GetService(serviceType);

    /// <inheritdoc />
    public TStore GetStore<TStore>()
        where TStore : IStore => StoreProvider.GetStore<TStore>();

    /// <inheritdoc />
    public async ValueTask<PagedResult<string>> GetKeyIdPageAsync(
        string provider,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var prefix = provider + ":";

        var query = DbContext
            .Secrets.AsNoTracking()
            .Where(entity =>
                entity.SecretEncoding == SecretEncodings.Remote
                && entity.EncodedValue.StartsWith(prefix)
            );

        var afterId = KeysetCursor.Decode(page.Cursor);
        if (afterId is { } id)
            query = query.Where(entity => entity.Id > id);

        // Fetch one extra row so a full page signals that another page exists.
        var entities = await query
            .OrderBy(entity => entity.Id)
            .Take(page.Limit + 1)
            .Select(entity => new { entity.Id, entity.EncodedValue })
            .ToListAsync(cancellationToken);

        var hasMore = entities.Count > page.Limit;
        var count = hasMore ? page.Limit : entities.Count;

        return new PagedResult<string>
        {
            Items = entities
                .Take(count)
                .Select(entity => entity.EncodedValue[prefix.Length..])
                .ToList(),
            NextCursor = hasMore ? KeysetCursor.Encode(entities[count - 1].Id) : null,
        };
    }
}
