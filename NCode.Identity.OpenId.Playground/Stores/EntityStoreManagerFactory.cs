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

using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Stores;

namespace NCode.Identity.OpenId.Playground.Stores;

/// <summary>
/// Provides an implementation for the <see cref="IStoreManagerFactory"/> abstraction that uses an <see cref="DbContext"/> instance
/// for the unit-of-work pattern.
/// </summary>
/// <typeparam name="TDbContext">The type of the <see cref="DbContext"/> instance.</typeparam>
public sealed class EntityStoreManagerFactory<TDbContext>(
    IServiceProvider serviceProvider
) : IStoreManagerFactory
    where TDbContext : DbContext
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <inheritdoc />
    public ValueTask<IStoreManager> CreateAsync(CancellationToken cancellationToken)
    {
        IStoreManager storeManager = ActivatorUtilities.CreateInstance<EntityStoreManager<TDbContext>>(ServiceProvider);
        return ValueTask.FromResult(storeManager);
    }
}
