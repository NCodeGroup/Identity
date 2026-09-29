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
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ServerStoreLifecycleTests : IDisposable
{
    private const string ServerId = "server-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly ServerStore _store;

    public ServerStoreLifecycleTests()
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
        _store = new ServerStore(
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

    private static PersistedSecret CreateSecret(string secretId) =>
        new()
        {
            SecretId = secretId,
            ConcurrencyToken = string.Empty,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Symmetric,
            KeySizeBits = 256,
            EncodedValue = "encoded-value",
        };

    private static PersistedServer CreateServer(params PersistedSecret[] secrets) =>
        new()
        {
            ServerId = ServerId,
            ConcurrencyToken = string.Empty,
            Settings = new PersistedServerSettings
            {
                ServerId = ServerId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedServerSecrets
            {
                ServerId = ServerId,
                ConcurrencyToken = string.Empty,
                Value = secrets,
            },
        };

    private async Task SeedServerAsync(params PersistedSecret[] secrets)
    {
        await _store.AddAsync(CreateServer(secrets), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_AssignsRowConcurrencyToken()
    {
        var server = CreateServer();

        await _store.AddAsync(server, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(server.ConcurrencyToken));
    }

    [Fact]
    public async Task RemoveAsync_WhenNoSecrets_RemovesAndReturnsTrue()
    {
        await SeedServerAsync();

        var removed = await _store.RemoveAsync(ServerId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await _store.GetOrDefaultAsync(ServerId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_WhenHasSecrets_Throws()
    {
        await SeedServerAsync(CreateSecret("secret-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveAsync(ServerId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveAsync_WhenMissing_ReturnsFalse()
    {
        var removed = await _store.RemoveAsync("missing", CancellationToken.None);

        Assert.False(removed);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenNoSecrets_ReturnsFalse()
    {
        await SeedServerAsync();

        var result = await _store.HasDependentsAsync(ServerId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenHasSecrets_ReturnsTrue()
    {
        await SeedServerAsync(CreateSecret("secret-1"));

        var result = await _store.HasDependentsAsync(ServerId, CancellationToken.None);

        Assert.True(result);
    }
}
