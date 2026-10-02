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

public sealed class FederatedIdentityStoreTests : IDisposable
{
    private const string PrincipalId = "principal-1";
    private const string Issuer = "https://issuer.example";
    private const string Subject = "upstream-subject-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly FederatedPrincipalStore _principalStore;
    private readonly FederatedIdentityStore _store;

    public FederatedIdentityStoreTests()
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
        _principalStore = new FederatedPrincipalStore(
            Mock.Of<IStoreProvider>(),
            idGenerator,
            _dbContext
        );
        _store = new FederatedIdentityStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedFederatedIdentity CreateIdentity(
        string federatedIdentityId,
        string principalId,
        string issuer,
        string subject
    ) =>
        new()
        {
            FederatedIdentityId = federatedIdentityId,
            PrincipalId = principalId,
            Issuer = issuer,
            Subject = subject,
            ConcurrencyToken = string.Empty,
        };

    private async Task SeedPrincipalAsync(string principalId = PrincipalId)
    {
        await _principalStore.AddAsync(
            new PersistedFederatedPrincipal
            {
                PrincipalId = principalId,
                ConcurrencyToken = string.Empty,
            },
            CancellationToken.None
        );

        // The principal must be persisted before its identities are added (an in-memory query does not observe
        // unsaved inserts).
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task AddAsync_WhenPersisted_RoundTripsByFederatedIdentityId()
    {
        await SeedPrincipalAsync();

        await _store.AddAsync(
            CreateIdentity("identity-1", PrincipalId, Issuer, Subject),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        var loaded = await _store.GetOrDefaultAsync("identity-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("identity-1", loaded.FederatedIdentityId);
        Assert.Equal(PrincipalId, loaded.PrincipalId);
        Assert.Equal(Issuer, loaded.Issuer);
        Assert.Equal(Subject, loaded.Subject);
        Assert.NotEmpty(loaded.ConcurrencyToken);
    }

    [Fact]
    public async Task AddAsync_WhenPrincipalMissing_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.AddAsync(
                CreateIdentity("identity-1", "missing-principal", Issuer, Subject),
                CancellationToken.None
            )
        );

        Assert.Contains("missing-principal", exception.Message);
    }

    [Fact]
    public async Task GetByIssuerSubjectAsync_WhenMatch_ResolvesToOwningPrincipal()
    {
        await SeedPrincipalAsync();
        await _store.AddAsync(
            CreateIdentity("identity-1", PrincipalId, Issuer, Subject),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        var loaded = await _store.GetByIssuerSubjectAsync(Issuer, Subject, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(PrincipalId, loaded.PrincipalId);
        Assert.Equal("identity-1", loaded.FederatedIdentityId);
    }

    [Fact]
    public async Task GetByIssuerSubjectAsync_WhenNoMatch_ReturnsNull()
    {
        await SeedPrincipalAsync();

        var loaded = await _store.GetByIssuerSubjectAsync(Issuer, Subject, CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetByPrincipalAsync_ReturnsAllIdentitiesOfThePrincipal()
    {
        await SeedPrincipalAsync();
        await _store.AddAsync(
            CreateIdentity("identity-1", PrincipalId, Issuer, Subject),
            CancellationToken.None
        );
        await _store.AddAsync(
            CreateIdentity("identity-2", PrincipalId, "https://other.example", "subject-2"),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();

        var loaded = await _store.GetByPrincipalAsync(PrincipalId, CancellationToken.None);

        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, identity => identity.FederatedIdentityId == "identity-1");
        Assert.Contains(loaded, identity => identity.FederatedIdentityId == "identity-2");
        Assert.All(loaded, identity => Assert.Equal(PrincipalId, identity.PrincipalId));
    }
}
