// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    private static ConcurrentDictionary<Type, MethodInvoker> StoreMethodInvokers { get; } = new();

    private static MethodInfo GetStoreMethod { get; } =
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
        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
        // See ADR-1208.
        catch (DbUpdateConcurrencyException exception)
        {
            throw new StoreConcurrencyException(
                "The store was changed by another unit of work after it was read.",
                exception
            );
        }
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
