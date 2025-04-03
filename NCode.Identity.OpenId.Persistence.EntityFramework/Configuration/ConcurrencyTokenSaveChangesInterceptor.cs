#region Copyright Preamble

// Copyright @ 2025 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;

/// <summary>
/// Provides an interceptor to automatically generate a new value for <see cref="string"/> concurrency tokens.
/// </summary>
public class ConcurrencyTokenSaveChangesInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var entries = eventData.Context?.ChangeTracker.Entries();
        if (entries is null)
        {
            return base.SavingChanges(eventData, result);
        }

        foreach (var entry in entries.Where(IsMutated))
        {
            var properties = entry.Properties.Where(IsConcurrencyToken);
            foreach (var property in properties)
            {
                property.CurrentValue = Guid.NewGuid().ToString("N");
            }
        }

        return base.SavingChanges(eventData, result);
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
