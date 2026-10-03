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

public sealed class ServerStoreSecretsTests : IDisposable
{
    private const string ServerId = "server-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly ServerStore _store;

    public ServerStoreSecretsTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        serviceCollection.AddSingleton<IdValueGenerator>();
        serviceCollection.AddSingleton<UseIdGeneratorConvention>();
        serviceCollection.AddDbContext<OpenIdDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
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

    private async Task SeedServerAsync(params PersistedSecret[] secrets)
    {
        var server = new PersistedServer
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

        await _store.AddAsync(server, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task<string> GetServerSecretsTokenAsync()
    {
        var server = await _dbContext
            .Servers.AsNoTracking()
            .SingleAsync(x => x.ServerId == ServerId);
        return server.SecretsConcurrencyToken;
    }

    #region GetSecretOrDefaultAsync Tests

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenExists_ReturnsSecret()
    {
        await SeedServerAsync(CreateSecret("secret-1"));

        var result = await _store.GetSecretOrDefaultAsync(
            ServerId,
            "secret-1",
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal("secret-1", result.SecretId);
        Assert.Equal(SecretTypes.Symmetric, result.SecretType);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenMissing_ReturnsNull()
    {
        await SeedServerAsync(CreateSecret("secret-1"));

        var result = await _store.GetSecretOrDefaultAsync(
            ServerId,
            "missing",
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion

    #region AddSecretAsync Tests

    [Fact]
    public async Task AddSecretAsync_WhenNew_AddsSecretAndBumpsServerToken()
    {
        await SeedServerAsync(CreateSecret("secret-1"));
        var tokenBefore = await GetServerSecretsTokenAsync();

        await _store.AddSecretAsync(ServerId, CreateSecret("secret-2"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var added = await _store.GetSecretOrDefaultAsync(
            ServerId,
            "secret-2",
            CancellationToken.None
        );
        Assert.NotNull(added);

        var tokenAfter = await GetServerSecretsTokenAsync();
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task AddSecretAsync_WhenDuplicate_Throws()
    {
        await SeedServerAsync(CreateSecret("secret-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.AddSecretAsync(ServerId, CreateSecret("secret-1"), CancellationToken.None)
        );
    }

    #endregion

    #region UpdateSecretAsync Tests

    [Fact]
    public async Task UpdateSecretAsync_WhenTokenMatches_UpdatesMetadata()
    {
        await SeedServerAsync(CreateSecret("secret-1"));
        var tokenBefore = await GetServerSecretsTokenAsync();

        var current = await _store.GetSecretOrDefaultAsync(
            ServerId,
            "secret-1",
            CancellationToken.None
        );
        Assert.NotNull(current);

        var updated = new PersistedSecret
        {
            SecretId = "secret-1",
            ConcurrencyToken = current.ConcurrencyToken,
            Use = "enc",
            Algorithm = "RS512",
            CreatedWhen = current.CreatedWhen,
            ExpiresWhen = current.ExpiresWhen.AddYears(1),
            SecretType = current.SecretType,
            KeySizeBits = current.KeySizeBits,
            EncodedValue = current.EncodedValue,
        };

        await _store.UpdateSecretAsync(ServerId, updated, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _store.GetSecretOrDefaultAsync(
            ServerId,
            "secret-1",
            CancellationToken.None
        );
        Assert.NotNull(reloaded);
        Assert.Equal("enc", reloaded.Use);
        Assert.Equal("RS512", reloaded.Algorithm);
        Assert.Equal(current.EncodedValue, reloaded.EncodedValue);
        Assert.NotEqual(current.ConcurrencyToken, reloaded.ConcurrencyToken);

        var tokenAfter = await GetServerSecretsTokenAsync();
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenTokenMismatch_Throws()
    {
        await SeedServerAsync(CreateSecret("secret-1"));

        var stale = CreateSecret("secret-1");
        stale.ConcurrencyToken = "stale-token";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            await _store.UpdateSecretAsync(ServerId, stale, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenMissing_Throws()
    {
        await SeedServerAsync(CreateSecret("secret-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.UpdateSecretAsync(
                ServerId,
                CreateSecret("missing"),
                CancellationToken.None
            )
        );
    }

    #endregion

    #region RemoveSecretAsync Tests

    [Fact]
    public async Task RemoveSecretAsync_WhenExists_RemovesAndReturnsTrue()
    {
        await SeedServerAsync(CreateSecret("secret-1"), CreateSecret("secret-2"));
        var tokenBefore = await GetServerSecretsTokenAsync();

        var removed = await _store.RemoveSecretAsync(ServerId, "secret-1", CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);

        var gone = await _store.GetSecretOrDefaultAsync(
            ServerId,
            "secret-1",
            CancellationToken.None
        );
        Assert.Null(gone);

        var tokenAfter = await GetServerSecretsTokenAsync();
        Assert.NotEqual(tokenBefore, tokenAfter);
    }

    [Fact]
    public async Task RemoveSecretAsync_WhenMissing_ReturnsFalse()
    {
        await SeedServerAsync(CreateSecret("secret-1"));

        var removed = await _store.RemoveSecretAsync(ServerId, "missing", CancellationToken.None);

        Assert.False(removed);
    }

    #endregion
}
