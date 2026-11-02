// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;

/// <summary>
/// Provides an interceptor to automatically generate a new value for <see cref="string"/> concurrency tokens.
/// </summary>
internal class ConcurrencyTokenSaveChangesInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        RegenerateConcurrencyTokens(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        RegenerateConcurrencyTokens(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void RegenerateConcurrencyTokens(DbContext? context)
    {
        var entries = context?.ChangeTracker.Entries();
        if (entries is null)
        {
            return;
        }

        foreach (var entry in entries.Where(IsMutated))
        {
            // On insert a caller may legitimately pre-seed the token (a new row cannot have a concurrency
            // conflict), so respect it; on update/delete always force a new value so the row version changes.
            var forceRegenerate = entry.State is not EntityState.Added;
            foreach (var property in entry.Properties.Where(IsConcurrencyToken))
            {
                if (forceRegenerate || string.IsNullOrEmpty((string?)property.CurrentValue))
                {
                    property.CurrentValue = Guid.NewGuid().ToString("N");
                }
            }
        }
    }

    private static bool IsMutated(EntityEntry entity)
    {
        return entity.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
    }

    private static bool IsConcurrencyToken(PropertyEntry property)
    {
        return property.Metadata.IsConcurrencyToken && property.Metadata.ClrType == typeof(string);
    }
}
