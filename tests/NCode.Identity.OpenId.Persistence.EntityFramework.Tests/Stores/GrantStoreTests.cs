#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class GrantStoreTests : IDisposable
{
    private const string GrantType = "authorization_code";
    private const string HashedKey = "hashed-key-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly GrantStore _store;

    public GrantStoreTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        services.AddSingleton<IdValueGenerator>();
        services.AddSingleton<UseIdGeneratorConvention>();
        services.AddDbContext<OpenIdDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = services.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        _store = new GrantStore(
            Mock.Of<IStoreProvider>(),
            _provider.GetRequiredService<IIdGenerator<long>>(),
            _dbContext
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedGrant CreateGrant() =>
        new()
        {
            GrantType = GrantType,
            HashedKey = HashedKey,
            ConcurrencyToken = string.Empty,
            TenantId = null,
            ClientId = null,
            SubjectId = "subject-1",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddHours(1),
            RevokedWhen = null,
            ConsumedWhen = null,
            PayloadJson = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
        };

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_AssignsConcurrencyToken()
    {
        await _store.AddAsync(CreateGrant(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _store.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.False(string.IsNullOrEmpty(reloaded.ConcurrencyToken));
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ChangesConcurrencyToken()
    {
        await _store.AddAsync(CreateGrant(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var current = await _store.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);
        Assert.NotNull(current);

        current.ConsumedWhen = DateTimeOffset.UnixEpoch.AddMinutes(5);
        await _store.UpdateAsync(current, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _store.GetOrDefaultAsync(GrantType, HashedKey, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.NotEqual(current.ConcurrencyToken, reloaded.ConcurrencyToken);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(5), reloaded.ConsumedWhen);
    }

    [Fact]
    public async Task UpdateAsync_WhenTokenMismatch_Throws()
    {
        await _store.AddAsync(CreateGrant(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var stale = CreateGrant();
        stale.ConcurrencyToken = "stale-token";
        stale.ConsumedWhen = DateTimeOffset.UnixEpoch.AddMinutes(5);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            await _store.UpdateAsync(stale, CancellationToken.None)
        );
    }

    #endregion
}
