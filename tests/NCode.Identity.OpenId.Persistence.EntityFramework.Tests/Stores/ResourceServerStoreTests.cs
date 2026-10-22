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

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ResourceServerStoreTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ResourceServerId = "rs-1";
    private const string Identifier = "https://api.example.com";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ResourceServerStore _store;

    public ResourceServerStoreTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _store = new ResourceServerStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedScope CreateScope(string value, bool isSystem = false) =>
        new()
        {
            Value = value,
            Description = $"{value} description",
            IsSystem = isSystem,
        };

    private static PersistedResourceServer CreateResourceServer(
        bool isSystem = false,
        params PersistedScope[] scopes
    ) =>
        new()
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            Identifier = Identifier,
            ConcurrencyToken = string.Empty,
            Name = "Example API",
            IsSystem = isSystem,
            IsDisabled = false,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = TenantId,
                ResourceServerId = ResourceServerId,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Scopes = scopes,
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = TenantId,
                ResourceServerId = ResourceServerId,
                Value = [],
            },
        };

    private async Task SeedTenantAsync()
    {
        var tenant = new PersistedTenant
        {
            TenantId = TenantId,
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = "Tenant One",
            Settings = new PersistedTenantSettings
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

        await _tenantStore.AddAsync(tenant, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #region AddAsync / GetOrDefaultAsync Tests

    [Fact]
    public async Task AddAsync_RoundtripsWithScopes()
    {
        await SeedTenantAsync();

        await _store.AddAsync(
            CreateResourceServer(scopes: [CreateScope("read:x"), CreateScope("write:x")]),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(Identifier, reloaded.Identifier);
        Assert.Equal("Example API", reloaded.Name);
        Assert.Equal(2, reloaded.Scopes.Count);
        Assert.False(string.IsNullOrEmpty(reloaded.ConcurrencyToken));
    }

    [Fact]
    public async Task GetByIdentifierOrDefaultAsync_ReturnsMatch()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var found = await _store.GetByIdentifierOrDefaultAsync(Identifier, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(ResourceServerId, found.ResourceServerId);
    }

    #endregion

    #region Scope Sub-Resource Tests

    [Fact]
    public async Task AddScopeAsync_AddsScope()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.AddScopeAsync(ResourceServerId, CreateScope("read:x"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded.Scopes);
        Assert.Equal("read:x", reloaded.Scopes.First().Value);
    }

    [Fact]
    public async Task RemoveScopeAsync_WhenSystemScope_Throws()
    {
        await SeedTenantAsync();
        await _store.AddAsync(
            CreateResourceServer(scopes: [CreateScope("openid", isSystem: true)]),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveScopeAsync(ResourceServerId, "openid", CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveScopeAsync_RemovesNonSystemScope()
    {
        await SeedTenantAsync();
        await _store.AddAsync(
            CreateResourceServer(scopes: [CreateScope("read:x")]),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveScopeAsync(
            ResourceServerId,
            "read:x",
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var reloaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Empty(reloaded.Scopes);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenSystem_Throws()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(isSystem: true), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveAsync(ResourceServerId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveAsync_RemovesNonSystem()
    {
        await SeedTenantAsync();
        await _store.AddAsync(
            CreateResourceServer(scopes: [CreateScope("read:x")]),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.RemoveAsync(ResourceServerId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    #endregion

    #region Settings Sub-Resource Tests

    [Fact]
    public async Task UpdateSettingsAsync_UpdatesValueAndBumpsToken()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var loaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);
        Assert.NotNull(loaded);
        var settings = loaded.Settings;
        var originalToken = settings.ConcurrencyToken;
        settings.Value = JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["token_lifetime"] = 3600 }
        );

        await _store.UpdateSettingsAsync(settings, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.NotEqual(originalToken, settings.ConcurrencyToken);
        var reloaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.Settings.Value.TryGetProperty("token_lifetime", out var lifetime));
        Assert.Equal(3600, lifetime.GetInt32());
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenConcurrencyMismatch_Throws()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var loaded = await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None);
        Assert.NotNull(loaded);
        var settings = loaded.Settings;
        settings.ConcurrencyToken = "stale";

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await _store.UpdateSettingsAsync(settings, CancellationToken.None)
        );
    }

    #endregion

    #region Secret Sub-Resource Tests

    private static PersistedSecret CreateSecret(string secretId) =>
        new()
        {
            SecretId = secretId,
            ConcurrencyToken = string.Empty,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UtcNow,
            ExpiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            SecretType = "rsa",
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.Pkcs8,
            KeySizeBits = 2048,
            EncodedValue = "encoded",
        };

    [Fact]
    public async Task AddSecretAsync_ThenGetSecrets_RoundtripsAndBumpsToken()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var before = await _store.GetSecretsOrDefaultAsync(
            ResourceServerId,
            CancellationToken.None
        );
        Assert.NotNull(before);
        var originalToken = before.ConcurrencyToken;

        await _store.AddSecretAsync(ResourceServerId, CreateSecret("s-1"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var secrets = await _store.GetSecretsOrDefaultAsync(
            ResourceServerId,
            CancellationToken.None
        );
        Assert.NotNull(secrets);
        Assert.Single(secrets.Value);
        Assert.Equal("s-1", secrets.Value.First().SecretId);
        Assert.NotEqual(originalToken, secrets.ConcurrencyToken);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_ReturnsScopedSecret()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.AddSecretAsync(ResourceServerId, CreateSecret("s-1"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var secret = await _store.GetSecretOrDefaultAsync(
            ResourceServerId,
            "s-1",
            CancellationToken.None
        );

        Assert.NotNull(secret);
        Assert.Equal(TenantId, secret.TenantId);
        Assert.Equal(ResourceServerId, secret.ResourceServerId);
        Assert.Equal("s-1", secret.Value.SecretId);
    }

    [Fact]
    public async Task UpdateSecretAsync_UpdatesMetadataOnly()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.AddSecretAsync(ResourceServerId, CreateSecret("s-1"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var secret = await _store.GetSecretOrDefaultAsync(
            ResourceServerId,
            "s-1",
            CancellationToken.None
        );
        Assert.NotNull(secret);
        var existing = secret.Value;
        var updated = new PersistedSecret
        {
            SecretId = existing.SecretId,
            ConcurrencyToken = existing.ConcurrencyToken,
            Use = "enc",
            Algorithm = existing.Algorithm,
            CreatedWhen = existing.CreatedWhen,
            ExpiresWhen = DateTimeOffset.UtcNow.AddYears(2),
            SecretType = existing.SecretType,
            SecretEncoding = existing.SecretEncoding,
            KeySizeBits = existing.KeySizeBits,
            EncodedValue = existing.EncodedValue,
        };

        await _store.UpdateSecretAsync(ResourceServerId, updated, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _store.GetSecretOrDefaultAsync(
            ResourceServerId,
            "s-1",
            CancellationToken.None
        );
        Assert.NotNull(reloaded);
        Assert.Equal("enc", reloaded.Value.Use);
    }

    [Fact]
    public async Task RemoveSecretAsync_RemovesSecret()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.AddSecretAsync(ResourceServerId, CreateSecret("s-1"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var removed = await _store.RemoveSecretAsync(
            ResourceServerId,
            "s-1",
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.True(removed);
        var secrets = await _store.GetSecretsOrDefaultAsync(
            ResourceServerId,
            CancellationToken.None
        );
        Assert.NotNull(secrets);
        Assert.Empty(secrets.Value);
    }

    [Fact]
    public async Task RemoveAsync_AlsoRemovesSecrets()
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateResourceServer(), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.AddSecretAsync(ResourceServerId, CreateSecret("s-1"), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await _store.RemoveAsync(ResourceServerId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Null(await _store.GetOrDefaultAsync(ResourceServerId, CancellationToken.None));
        Assert.Null(
            await _store.GetSecretsOrDefaultAsync(ResourceServerId, CancellationToken.None)
        );
    }

    #endregion

    #region Runtime Query Tests

    private async Task AddServerAsync(
        string resourceServerId,
        string identifier,
        bool isSystem,
        bool isDisabled,
        params string[] scopes
    )
    {
        var resourceServer = new PersistedResourceServer
        {
            TenantId = TenantId,
            ResourceServerId = resourceServerId,
            Identifier = identifier,
            ConcurrencyToken = string.Empty,
            Name = resourceServerId,
            IsSystem = isSystem,
            IsDisabled = isDisabled,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = TenantId,
                ResourceServerId = resourceServerId,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Scopes = scopes.Select(scope => CreateScope(scope)).ToList(),
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = TenantId,
                ResourceServerId = resourceServerId,
                Value = [],
            },
        };

        await _store.AddAsync(resourceServer, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetEnabledScopesAsync_WhenNoFilter_ReturnsAllEnabledScopes()
    {
        await SeedTenantAsync();
        await AddServerAsync(
            "rs-a",
            "urn:a",
            isSystem: false,
            isDisabled: false,
            "read:a",
            "write:a"
        );
        await AddServerAsync("rs-dead", "urn:dead", isSystem: false, isDisabled: true, "read:dead");

        var scopes = await _store.GetEnabledScopesAsync(scopeValues: null, CancellationToken.None);

        var values = scopes.Select(scope => scope.Value).ToHashSet();
        Assert.Contains("read:a", values);
        Assert.Contains("write:a", values);
        Assert.DoesNotContain("read:dead", values);
    }

    [Fact]
    public async Task GetEnabledScopesAsync_WhenFiltered_ReturnsOnlyMatchingValues()
    {
        await SeedTenantAsync();
        await AddServerAsync(
            "rs-a",
            "urn:a",
            isSystem: false,
            isDisabled: false,
            "read:a",
            "write:a"
        );

        var scopes = await _store.GetEnabledScopesAsync(["read:a"], CancellationToken.None);

        Assert.Single(scopes);
        Assert.Equal("read:a", scopes.First().Value);
    }

    [Fact]
    public async Task GetEnabledIdentifiersAsync_ExcludesDisabled()
    {
        await SeedTenantAsync();
        await AddServerAsync("rs-a", "urn:a", isSystem: false, isDisabled: false, "read:a");
        await AddServerAsync("rs-dead", "urn:dead", isSystem: false, isDisabled: true, "read:dead");

        var identifiers = await _store.GetEnabledIdentifiersAsync(CancellationToken.None);

        Assert.Equal(["urn:a"], identifiers);
    }

    [Fact]
    public async Task GetEnabledScopeProjectionsByScopesAsync_ReturnsOwningEnabledServers()
    {
        await SeedTenantAsync();
        await AddServerAsync(
            "rs-a",
            "urn:a",
            isSystem: false,
            isDisabled: false,
            "read:x",
            "write:a"
        );
        await AddServerAsync("rs-b", "urn:b", isSystem: false, isDisabled: false, "read:b");
        await AddServerAsync("rs-dead", "urn:dead", isSystem: false, isDisabled: true, "read:x");

        var projections = await _store.GetEnabledScopeProjectionsByScopesAsync(
            ["read:x"],
            CancellationToken.None
        );

        var projection = Assert.Single(projections);
        Assert.Equal("rs-a", projection.ResourceServerId);
        Assert.Contains("read:x", projection.ScopeValues);
    }

    [Fact]
    public async Task GetEnabledScopeProjectionsBySystemOrIdsAsync_ReturnsSystemAndGranted()
    {
        await SeedTenantAsync();
        await AddServerAsync("rs-sys", "urn:sys", isSystem: true, isDisabled: false, "openid");
        await AddServerAsync(
            "rs-granted",
            "urn:granted",
            isSystem: false,
            isDisabled: false,
            "read:g"
        );
        await AddServerAsync("rs-other", "urn:other", isSystem: false, isDisabled: false, "read:o");
        await AddServerAsync("rs-dead", "urn:dead", isSystem: true, isDisabled: true, "dead");

        var projections = await _store.GetEnabledScopeProjectionsBySystemOrIdsAsync(
            ["rs-granted"],
            CancellationToken.None
        );

        var ids = projections.Select(projection => projection.ResourceServerId).ToHashSet();
        Assert.Contains("rs-sys", ids);
        Assert.Contains("rs-granted", ids);
        Assert.DoesNotContain("rs-other", ids);
        Assert.DoesNotContain("rs-dead", ids);
    }

    [Fact]
    public async Task FindEnabledByScopesAsync_ReturnsOwningEnabledServersWithSecrets()
    {
        await SeedTenantAsync();
        await AddServerAsync("rs-a", "urn:a", isSystem: false, isDisabled: false, "read:x");
        await AddServerAsync("rs-b", "urn:b", isSystem: false, isDisabled: false, "read:y");
        await AddServerAsync("rs-dead", "urn:dead", isSystem: false, isDisabled: true, "read:x");

        var matches = await _store.FindEnabledByScopesAsync(
            ["read:x"],
            identifiers: null,
            CancellationToken.None
        );

        var match = Assert.Single(matches);
        Assert.Equal("rs-a", match.ResourceServerId);
        Assert.NotNull(match.Secrets);
    }

    [Fact]
    public async Task FindEnabledByScopesAsync_WhenIdentifiersProvided_NarrowsByIdentifier()
    {
        await SeedTenantAsync();
        await AddServerAsync("rs-a", "urn:a", isSystem: false, isDisabled: false, "read:x");
        await AddServerAsync("rs-b", "urn:b", isSystem: false, isDisabled: false, "read:x");
        await AddServerAsync("rs-c", "urn:c", isSystem: false, isDisabled: false, "read:x");

        var matches = await _store.FindEnabledByScopesAsync(
            ["read:x"],
            ["urn:a", "urn:b"],
            CancellationToken.None
        );

        var identifiers = matches.Select(match => match.Identifier).ToList();
        Assert.Equal(["urn:a", "urn:b"], identifiers);
    }

    #endregion
}
