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

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class FederatedPrincipalStoreTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly FederatedPrincipalStore _store;

    public FederatedPrincipalStoreTests()
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
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new FederatedPrincipalStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedFederatedPrincipal CreatePrincipal(string principalId) =>
        new() { PrincipalId = principalId, ConcurrencyToken = string.Empty };

    [Fact]
    public async Task AddAsync_WhenPersisted_RoundTripsByPrincipalId()
    {
        await _store.AddAsync(CreatePrincipal("principal-1"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var loaded = await _store.GetOrDefaultAsync("principal-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("principal-1", loaded.PrincipalId);
        Assert.NotEmpty(loaded.ConcurrencyToken);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenCaseDiffers_IsCaseInsensitive()
    {
        await _store.AddAsync(CreatePrincipal("Principal-1"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var loaded = await _store.GetOrDefaultAsync("principal-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Principal-1", loaded.PrincipalId);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        var loaded = await _store.GetOrDefaultAsync("nope", CancellationToken.None);

        Assert.Null(loaded);
    }
}
