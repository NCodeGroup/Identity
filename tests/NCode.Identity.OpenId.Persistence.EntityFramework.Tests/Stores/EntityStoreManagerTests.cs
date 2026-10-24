#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class EntityStoreManagerTests
{
    private sealed class FailingDbContext(Exception exception) : DbContext
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<int>(exception);
    }

    private sealed class FailingDbContextFactory(FailingDbContext dbContext)
        : IDbContextFactory<FailingDbContext>
    {
        public FailingDbContext CreateDbContext() => dbContext;
    }

    private static EntityStoreManager<FailingDbContext> CreateStoreManager(Exception exception) =>
        new(
            Mock.Of<IServiceProvider>(),
            new FailingDbContextFactory(new FailingDbContext(exception))
        );

    #region SaveChangesAsync Tests

    [Fact]
    public async Task SaveChangesAsync_WhenAConcurrencyCheckFails_ThrowsStoreConcurrencyException()
    {
        var concurrencyException = new DbUpdateConcurrencyException("conflict");
        await using var storeManager = CreateStoreManager(concurrencyException);

        var exception = await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await storeManager.SaveChangesAsync(CancellationToken.None)
        );

        Assert.Same(concurrencyException, exception.InnerException);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenTheSaveFailsOtherwise_RethrowsTheException()
    {
        var updateException = new DbUpdateException("failure");
        await using var storeManager = CreateStoreManager(updateException);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await storeManager.SaveChangesAsync(CancellationToken.None)
        );

        Assert.Same(updateException, exception);
    }

    #endregion
}
