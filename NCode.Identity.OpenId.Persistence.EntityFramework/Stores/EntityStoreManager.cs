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
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

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
    private ConcurrentDictionary<Type, IStore> Stores { get; } = new();

    // ReSharper disable once StaticMemberInGenericType
    private static ConcurrentDictionary<Type, MethodInvoker>? GetStoreMethodInvokers { get; set; }

    private static readonly MethodInfo GetStoreMethod = typeof(EntityStoreManager<TDbContext>).GetMethod(nameof(GetStore)) ??
                                                        throw new InvalidOperationException($"Method {nameof(GetStore)} not found.");

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
        var factory = ServiceProvider.GetRequiredService<Func<IStoreProvider, TDbContext, TStore>>();
        return factory(this, DbContext);
    }

    /// <inheritdoc />
    public async ValueTask SaveChangesAsync(CancellationToken cancellationToken)
    {
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    private object? GetStoreNonGeneric(Type storeType)
    {
        var methodInvokers = GetStoreMethodInvokers ??= new ConcurrentDictionary<Type, MethodInvoker>();
        var methodInvoker = methodInvokers.GetOrAdd(storeType, CreateGetStoreMethodInvoker);
        return methodInvoker.Invoke(this);
    }

    private static MethodInvoker CreateGetStoreMethodInvoker(Type storeType)
    {
        var closedGenericMethod = GetStoreMethod.MakeGenericMethod(storeType);
        return MethodInvoker.Create(closedGenericMethod);
    }
}
