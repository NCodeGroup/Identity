// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides an implementation for the <see cref="IStoreManagerFactory"/> abstraction that uses an <see cref="DbContext"/> instance
/// for the unit-of-work pattern.
/// </summary>
/// <typeparam name="TDbContext">The type of the <see cref="DbContext"/> instance.</typeparam>
internal sealed class EntityStoreManagerFactory<TDbContext>(IServiceProvider serviceProvider)
    : IStoreManagerFactory
    where TDbContext : DbContext
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <inheritdoc />
    public ValueTask<IStoreManager> CreateAsync(CancellationToken cancellationToken)
    {
        IStoreManager storeManager = ActivatorUtilities.CreateInstance<
            EntityStoreManager<TDbContext>
        >(ServiceProvider);
        return ValueTask.FromResult(storeManager);
    }
}
