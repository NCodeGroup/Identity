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

    private static PersistedGrant CreateGrant(
        string hashedKey = HashedKey,
        string grantId = "grant-1",
        string? subjectId = "subject-1"
    ) =>
        new()
        {
            GrantType = GrantType,
            HashedKey = hashedKey,
            GrantId = grantId,
            ConcurrencyToken = string.Empty,
            TenantId = null,
            ClientId = null,
            SubjectId = subjectId,
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

    [Fact]
    public async Task AddAsync_PersistsGrantId()
    {
        await _store.AddAsync(CreateGrant(grantId: "grant-abc"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync("grant-abc", CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal("grant-abc", reloaded.GrantId);
    }

    #endregion

    #region GetOrDefaultAsync Tests

    [Fact]
    public async Task GetOrDefaultAsync_ByGrantId_WhenMissing_ReturnsNull()
    {
        var result = await _store.GetOrDefaultAsync("no-such-grant", CancellationToken.None);

        Assert.Null(result);
    }

    #endregion

    #region GetPageAsync Tests

    [Fact]
    public async Task GetPageAsync_KeysetPaginatesInOrder()
    {
        for (var index = 0; index < 3; index++)
        {
            await _store.AddAsync(
                CreateGrant(hashedKey: $"hashed-key-{index}", grantId: $"grant-{index}"),
                CancellationToken.None
            );
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var firstPage = await _store.GetPageAsync(
            subjectId: null,
            clientId: null,
            cursor: null,
            limit: 2,
            CancellationToken.None
        );

        Assert.Equal(2, firstPage.Items.Count);
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await _store.GetPageAsync(
            subjectId: null,
            clientId: null,
            firstPage.NextCursor,
            limit: 2,
            CancellationToken.None
        );

        Assert.Single(secondPage.Items);
        Assert.Null(secondPage.NextCursor);

        var seen = firstPage.Items.Concat(secondPage.Items).Select(grant => grant.GrantId).ToList();
        Assert.Equal(new[] { "grant-0", "grant-1", "grant-2" }, seen);
    }

    [Fact]
    public async Task GetPageAsync_FiltersBySubject()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-a", grantId: "grant-a", subjectId: "alice"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-b", grantId: "grant-b", subjectId: "bob"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var page = await _store.GetPageAsync(
            subjectId: "alice",
            clientId: null,
            cursor: null,
            limit: 50,
            CancellationToken.None
        );

        Assert.Single(page.Items);
        Assert.Equal("grant-a", page.Items[0].GrantId);
    }

    #endregion

    #region RevokeWhereAsync Tests

    [Fact]
    public async Task RevokeWhereAsync_BySubject_RevokesOnlyActiveMatches()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-a1", grantId: "grant-a1", subjectId: "alice"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-a2", grantId: "grant-a2", subjectId: "alice"),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-b", grantId: "grant-b", subjectId: "bob"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var revokedWhen = DateTimeOffset.UnixEpoch.AddMinutes(10);
        var count = await _store.RevokeWhereAsync(
            subjectId: "alice",
            clientId: null,
            revokedWhen,
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(2, count);

        var alice1 = await _store.GetOrDefaultAsync("grant-a1", CancellationToken.None);
        var bob = await _store.GetOrDefaultAsync("grant-b", CancellationToken.None);
        Assert.NotNull(alice1);
        Assert.NotNull(bob);
        Assert.Equal(revokedWhen, alice1.RevokedWhen);
        Assert.Null(bob.RevokedWhen);
    }

    [Fact]
    public async Task RevokeWhereAsync_IsIdempotentForAlreadyRevoked()
    {
        await _store.AddAsync(
            CreateGrant(hashedKey: "hk-a", grantId: "grant-a", subjectId: "alice"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var first = await _store.RevokeWhereAsync(
            subjectId: "alice",
            clientId: null,
            DateTimeOffset.UnixEpoch.AddMinutes(10),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var second = await _store.RevokeWhereAsync(
            subjectId: "alice",
            clientId: null,
            DateTimeOffset.UnixEpoch.AddMinutes(20),
            CancellationToken.None
        );

        Assert.Equal(1, first);
        Assert.Equal(0, second);
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
