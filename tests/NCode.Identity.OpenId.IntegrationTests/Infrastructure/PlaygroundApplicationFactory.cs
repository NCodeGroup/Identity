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

using System.Buffers;
using System.Buffers.Text;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Extensions.DataProtection;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Playground;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> that boots the <c>Playground</c> host in-process
/// through the full ASP.NET Core HTTP pipeline (via <see cref="TestServer"/>), providing an
/// <see cref="System.Net.Http.HttpClient"/> for over-the-wire integration tests.
/// </summary>
/// <remarks>
/// Each factory instance uses its own uniquely-named in-memory database so tests are fully isolated
/// (EF Core caches its internal service provider by option configuration, so a shared database name
/// would cause parallel test hosts to collide). At present the tenant has no signing keys; once an
/// ephemeral development key composition exists, this factory (or a derived one) can apply it so that
/// token-signing and a non-empty JWKS can be asserted.
/// </remarks>
public class PlaygroundApplicationFactory : WebApplicationFactory<PlaygroundApiMarker>
{
    /// <summary>The tenant id used by the Playground's static single-tenant provider.</summary>
    public const string TenantId = "default";

    private string DatabaseName { get; } = $"it-{Guid.NewGuid():N}";

    /// <summary>
    /// Ensures the tenant exists (persisted lazily on first request) and seeds a public client
    /// (<c>none</c> client authentication, no secrets) registered with the given redirect URIs.
    /// </summary>
    public async Task SeedPublicClientAsync(string clientId, params string[] redirectUris)
    {
        // A first request forces the static tenant provider to persist the tenant entity,
        // which ClientStore.AddAsync requires as a foreign key.
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        var settingsJson = System.Text.Json.JsonSerializer.SerializeToElement(
            new Dictionary<string, object> { ["redirect_uris"] = redirectUris }
        );

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IClientStore>();

        var persistedClient = new PersistedClient
        {
            TenantId = TenantId,
            ClientId = clientId,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = clientId,
                Value = settingsJson,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = clientId,
                Value = [],
            },
        };

        await store.AddAsync(persistedClient, CancellationToken.None);
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client (<c>client_secret_post</c>) whose symmetric
    /// secret matches <paramref name="clientSecret"/>, so the <c>client_credentials</c> grant can be exercised.
    /// </summary>
    public async Task SeedConfidentialClientAsync(string clientId, string clientSecret)
    {
        // A first request forces the static tenant provider to persist the tenant entity,
        // which ClientStore.AddAsync requires as a foreign key.
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        // Protect the secret the same way DefaultSecretSerializer expects to unprotect it.
        var protector = serviceProvider
            .GetRequiredService<IDataProtectorFactory<PersistedSecret>>()
            .CreateDataProtector();

        var secretBytes = Encoding.UTF8.GetBytes(clientSecret);
        var writer = new ArrayBufferWriter<byte>();
        protector.ProtectSpan(secretBytes, ref writer);

        var persistedSecret = new PersistedSecret
        {
            SecretId = $"{clientId}-secret",
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
            SecretType = SecretTypes.Symmetric,
            KeySizeBits = secretBytes.Length * 8,
            EncodedValue = Base64Url.EncodeToString(writer.WrittenSpan),
        };

        var emptySettingsJson = System.Text.Json.JsonSerializer.SerializeToElement(
            new Dictionary<string, object>()
        );

        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IClientStore>();

        var persistedClient = new PersistedClient
        {
            TenantId = TenantId,
            ClientId = clientId,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = clientId,
                Value = emptySettingsJson,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = clientId,
                Value = [persistedSecret],
            },
        };

        await store.AddAsync(persistedClient, CancellationToken.None);
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Seeds a resource server owning the given scopes and a client grant authorizing the client to all of them,
    /// so the client may request those scopes (replaces the retired <c>scopes_supported</c> setting).
    /// </summary>
    public async Task SeedResourceServerWithClientGrantAsync(
        string clientId,
        string resourceServerId,
        string identifier,
        params string[] scopes
    )
    {
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        var emptySettingsJson = System.Text.Json.JsonSerializer.SerializeToElement(
            new Dictionary<string, object>()
        );

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();
        var clientGrantStore = storeManager.GetStore<IClientGrantStore>();

        await resourceServerStore.AddAsync(
            new PersistedResourceServer
            {
                TenantId = TenantId,
                ResourceServerId = resourceServerId,
                Identifier = identifier,
                ConcurrencyToken = string.Empty,
                Name = resourceServerId,
                IsSystem = false,
                IsDisabled = false,
                Settings = emptySettingsJson,
                Scopes = scopes
                    .Select(value => new PersistedScope
                    {
                        Value = value,
                        Description = null,
                        IsSystem = false,
                    })
                    .ToList(),
            },
            CancellationToken.None
        );
        // Persist the resource server before the grant: the grant store resolves the resource-server foreign key,
        // and an in-memory query does not observe unsaved inserts.
        await storeManager.SaveChangesAsync(CancellationToken.None);

        await clientGrantStore.AddAsync(
            new PersistedClientGrant
            {
                TenantId = TenantId,
                ClientId = clientId,
                ResourceServerId = resourceServerId,
                ConcurrencyToken = string.Empty,
                Scopes = scopes,
            },
            CancellationToken.None
        );
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Resolve the content root to the Playground project so its appsettings.json is loaded.
        builder.UseSolutionRelativeContentRoot("src/NCode.Identity.OpenId.Playground");
        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Development);

        builder.ConfigureTestServices(services =>
        {
            // Replace the Playground's shared in-memory database with a per-factory isolated one.
            services.RemoveAll<DbContextOptions<OpenIdDbContext>>();
            services.RemoveAll<IDbContextFactory<OpenIdDbContext>>();

            var databaseRoot = new InMemoryDatabaseRoot();
            services.AddDbContextFactory<OpenIdDbContext>(options =>
                options.UseInMemoryDatabase(DatabaseName, databaseRoot)
            );

            // Widen the server ceiling (registered after the library default, so it unions on top):
            // enables the client_credentials grant and a custom "api" scope for integration coverage.
            services.AddSingleton<IDefaultSettingsProvider, TestServerSettingsProvider>();
        });
    }
}
