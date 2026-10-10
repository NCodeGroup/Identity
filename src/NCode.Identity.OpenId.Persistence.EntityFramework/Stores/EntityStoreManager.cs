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
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides an implementation for the <see cref="IStoreManager"/> abstraction that uses an <see cref="DbContext"/> instance
/// for the unit-of-work pattern.
/// </summary>
/// <typeparam name="TDbContext">The type of the <see cref="DbContext"/> instance.</typeparam>
internal sealed class EntityStoreManager<TDbContext>(
    IServiceProvider serviceProvider,
    IDbContextFactory<TDbContext> contextFactory
) : IStoreManager
    where TDbContext : DbContext
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private TDbContext DbContext { get; } = contextFactory.CreateDbContext();
    private ConcurrentDictionary<Type, IStore> Stores { get; } = new();

    // ReSharper disable once StaticMemberInGenericType
    private static readonly ConcurrentDictionary<Type, MethodInvoker> StoreMethodInvokers = new();

    private static readonly MethodInfo GetStoreMethod =
        typeof(EntityStoreManager<TDbContext>).GetMethod(nameof(GetStore))
        ?? throw new InvalidOperationException($"Method {nameof(GetStore)} not found.");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IStoreProvider) || serviceType == typeof(IStoreManager))
            return this;

        if (serviceType == typeof(DbContext) || serviceType == typeof(TDbContext))
            return DbContext;

        if (typeof(IStore).IsAssignableFrom(serviceType))
            return GetStoreNonGeneric(serviceType);

        return ServiceProvider.GetService(serviceType);
    }

    /// <inheritdoc />
    public TStore GetStore<TStore>()
        where TStore : IStore
    {
        return (TStore)Stores.GetOrAdd(typeof(TStore), _ => CreateStore<TStore>());
    }

    private TStore CreateStore<TStore>()
        where TStore : IStore
    {
        // A store that participates in this unit of work's DbContext registers the DbContext-bound factory. A
        // backing-store-agnostic store (for example, one over an external user store) registers the DbContext-less
        // factory instead, so it need not reference the concrete DbContext type.
        var boundFactory = ServiceProvider.GetService<Func<IStoreProvider, TDbContext, TStore>>();
        if (boundFactory is not null)
        {
            return boundFactory(this, DbContext);
        }

        var unboundFactory = ServiceProvider.GetService<Func<IStoreProvider, TStore>>();
        if (unboundFactory is not null)
        {
            return unboundFactory(this);
        }

        throw new InvalidOperationException(
            $"No store factory is registered for '{typeof(TStore).FullName}'."
        );
    }

    /// <inheritdoc />
    public async ValueTask SaveChangesAsync(CancellationToken cancellationToken)
    {
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    private object? GetStoreNonGeneric(Type storeType)
    {
        var methodInvoker = StoreMethodInvokers.GetOrAdd(storeType, CreateGetStoreMethodInvoker);
        return methodInvoker.Invoke(this);
    }

    private static MethodInvoker CreateGetStoreMethodInvoker(Type storeType)
    {
        var closedGenericMethod = GetStoreMethod.MakeGenericMethod(storeType);
        return MethodInvoker.Create(closedGenericMethod);
    }
}
