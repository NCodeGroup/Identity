#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Playground.Stores;

/// <summary>
/// Provides an implementation for the <see cref="IStoreManager"/> abstraction that uses an <see cref="DbContext"/> instance
/// for the unit-of-work pattern.
/// </summary>
/// <typeparam name="TDbContext">The type of the <see cref="DbContext"/> instance.</typeparam>
public sealed class EntityStoreManager<TDbContext>(
    IServiceProvider serviceProvider,
    IDbContextFactory<TDbContext> contextFactory
) : IStoreManager
    where TDbContext : DbContext
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private TDbContext DbContext { get; } = contextFactory.CreateDbContext();
    private ConcurrentDictionary<Type, object> Stores { get; } = new();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(DbContext) || serviceType == typeof(TDbContext))
            return DbContext;

        return ServiceProvider.GetService(serviceType);
    }

    /// <inheritdoc />
    public TStore GetStore<TStore>()
        where TStore : class
    {
        return (TStore)Stores.GetOrAdd(typeof(TStore), _ => CreateStore<TStore>());
    }

    private TStore CreateStore<TStore>()
        where TStore : class
    {
        var factory = ServiceProvider.GetRequiredService<Func<TDbContext, TStore>>();
        return factory(DbContext);
    }

    /// <inheritdoc />
    public async ValueTask SaveChangesAsync(CancellationToken cancellationToken)
    {
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
