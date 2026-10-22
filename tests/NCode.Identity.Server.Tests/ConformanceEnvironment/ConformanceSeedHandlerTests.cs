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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Server.ConformanceEnvironment;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class ConformanceSeedHandlerTests : BaseTests
{
    private const string WorkloadTenantId = "default";

    private ConformanceSeedHandler CreateHandler(ConformanceSeedOptions options) =>
        CreateHandler(
            options,
            CreateStrictMock<ISettingSerializer>().Object,
            CreateStrictMock<IOpenIdEnvironmentProvider>().Object
        );

    private ConformanceSeedHandler CreateHandler(
        ConformanceSeedOptions options,
        ISettingSerializer settingSerializer,
        IOpenIdEnvironmentProvider openIdEnvironmentProvider,
        IClientKeyImporter? clientKeyImporter = null,
        RedirectUriPolicy? redirectUriPolicy = null
    )
    {
        // Strict mocks: on the guard paths under test the secret, crypto, and time dependencies are untouched.
        var mockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        var mockCryptoService = CreateStrictMock<ICryptoService>();

        redirectUriPolicy ??= new RedirectUriPolicy(
            CreateStrictMock<IOpenIdServerProvider>().Object,
            CreateStrictMock<ISecurityProfileCatalog>().Object,
            CreateStrictMock<IRedirectUriKindCatalog>().Object
        );

        return new ConformanceSeedHandler(
            options,
            mockSecretGenerator.Object,
            clientKeyImporter ?? CreateStrictMock<IClientKeyImporter>().Object,
            mockCryptoService.Object,
            TimeProvider.System,
            settingSerializer,
            openIdEnvironmentProvider,
            redirectUriPolicy.ServerProvider,
            redirectUriPolicy.ProfileCatalog,
            redirectUriPolicy.KindCatalog,
            NullLogger<ConformanceSeedHandler>.Instance
        );
    }

    private sealed record RedirectUriPolicy(
        IOpenIdServerProvider ServerProvider,
        ISecurityProfileCatalog ProfileCatalog,
        IRedirectUriKindCatalog KindCatalog
    );

    private RedirectUriPolicy CreateRedirectUriPolicy(bool isPermitted)
    {
        var mockCollection = CreateLooseMock<ISettingCollection>();
        mockCollection
            .Setup(x => x.Merge(It.IsAny<IEnumerable<Setting>>()))
            .Returns(mockCollection.Object);

        var mockSettingsProvider = CreateLooseMock<IReadOnlySettingCollectionProvider>();
        mockSettingsProvider.Setup(x => x.Collection).Returns(mockCollection.Object);

        var mockServer = CreateLooseMock<OpenIdServer>();
        mockServer.Setup(x => x.SettingsProvider).Returns(mockSettingsProvider.Object);

        var mockServerProvider = CreateStrictMock<IOpenIdServerProvider>();
        mockServerProvider
            .Setup(x => x.GetAsync(It.IsAny<OpenIdEnvironment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockServer.Object)
            .Verifiable();

        var mockProfileCatalog = CreateStrictMock<ISecurityProfileCatalog>();
        mockProfileCatalog
            .Setup(x =>
                x.Expand(
                    It.IsAny<JsonElement>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns(Array.Empty<Setting>())
            .Verifiable();

        var mockKindCatalog = CreateStrictMock<IRedirectUriKindCatalog>();
        mockKindCatalog
            .Setup(x => x.IsPermitted(mockCollection.Object, It.IsAny<string>()))
            .Returns(isPermitted)
            .Verifiable();

        return new RedirectUriPolicy(
            mockServerProvider.Object,
            mockProfileCatalog.Object,
            mockKindCatalog.Object
        );
    }

    private SeedTenantCommand CreateCommand(
        string tenantId,
        TenantPlane plane,
        IStoreManager? storeManager = null
    )
    {
        // Strict mock: the store manager is untouched on the guard paths under test.
        storeManager ??= CreateStrictMock<IStoreManager>().Object;

        var persistedTenant = new PersistedTenant
        {
            TenantId = tenantId,
            Issuer = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                Value = JsonElement.Parse("{}"),
            },
            Secrets = null!,
        };

        return new SeedTenantCommand(
            new TenantSeedContext
            {
                Tenant = persistedTenant,
                Plane = plane,
                StoreManager = storeManager,
            }
        );
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNothingConfiguredToSeed_DoesNothing()
    {
        var handler = CreateHandler(
            new ConformanceSeedOptions { Clients = [], TestUserPassword = null }
        );

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNoTenantIsNamed_DoesNothing()
    {
        var handler = CreateHandler(
            new ConformanceSeedOptions { TestUserPassword = "conformance-password" }
        );

        await handler.HandleAsync(CreateCommand("root", TenantPlane.Root), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantDoesNotMatchConfiguredTarget_DoesNothing()
    {
        var handler = CreateHandler(
            new ConformanceSeedOptions
            {
                TestUserPassword = "conformance-password",
                TenantId = "tenant-a",
            }
        );

        await handler.HandleAsync(
            CreateCommand("tenant-b", TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTestUserConfigured_SeedsAccountWithProfileClaims()
    {
        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients = [],
            TestUserName = "conformance-user",
            TestUserEmail = "conformance-user@localhost",
            TestUserGivenName = "Conformance",
            TestUserFamilyName = "User",
            TestUserPassword = "conformance-password",
        };
        var handler = CreateHandler(options);

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockAccountStore = CreateStrictMock<ILocalAccountStore>();
        mockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(mockAccountStore.Object)
            .Verifiable();
        mockAccountStore
            .Setup(x =>
                x.GetByUserNameOrDefaultAsync("conformance-user", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedLocalAccount?)null)
            .Verifiable();

        PersistedLocalAccount? captured = null;
        mockAccountStore
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<PersistedLocalAccount>(),
                    It.IsAny<ReadOnlyMemory<byte>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (PersistedLocalAccount account, ReadOnlyMemory<byte>? _, CancellationToken _) =>
                    captured = account
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("conformance-user@localhost", captured.Email);
        Assert.True(captured.EmailVerified);

        var claims = captured.Claims.ToDictionary(claim => claim.Type, claim => claim.Value);
        Assert.Equal("Conformance", claims[JoseClaimNames.Payload.GivenName]);
        Assert.Equal("User", claims[JoseClaimNames.Payload.FamilyName]);
        Assert.Equal("Conformance User", claims[JoseClaimNames.Payload.Name]);
    }

    [Fact]
    public async Task HandleAsync_WhenClientSettingsCannotBeHonored_ThrowsNamingTheClient()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Returns([
                new SettingError(
                    "grant_types_supported",
                    "The setting 'grant_types_supported' must list only grant types."
                ),
            ])
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "client",
                    ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0LWNsaWVudA",
                    RedirectUris = ["https://client.example/callback"],
                    GrantTypes = ["authorisation_code"],
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object
        );

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'client'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'grant_types_supported'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenClientConfigured_RegistersItsMethodAndAppliesDefaults()
    {
        // The registered token_endpoint_auth_method decides the client type, so a seeded client is confidential the
        // way a dynamically registered one is, and it is the only method the client may use — the registration alone
        // says so, without a per-client token_endpoint_auth_methods_supported. Unconfigured lists take their
        // defaults: the Basic OP grant types, and the server's PKCE methods (the setting is left out).
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        // The validation result stops the seeding before the secret is stored, once the document is captured.
        JsonElement? capturedSettings = null;
        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Callback(
                (JsonElement settings, string _, JsonSerializerOptions _) =>
                    capturedSettings = settings
            )
            .Returns([new SettingError("redirect_uris", "stop")])
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "client",
                    ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0LWNsaWVudA",
                    RedirectUris = ["https://client.example/callback"],
                    TokenEndpointAuthMethod = "client_secret_post",
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object
        );

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.NotNull(capturedSettings);
        var settingsDocument = capturedSettings.Value;
        Assert.Equal(
            "client_secret_post",
            settingsDocument.GetProperty("token_endpoint_auth_method").GetString()
        );
        Assert.False(
            settingsDocument.TryGetProperty("token_endpoint_auth_methods_supported", out _)
        );
        Assert.Equal(
            ["authorization_code", "refresh_token"],
            GetStrings(settingsDocument, "grant_types_supported")
        );
        Assert.False(settingsDocument.TryGetProperty("code_challenge_methods_supported", out _));
    }

    [Fact]
    public async Task HandleAsync_WhenClientHasOnlyKeySet_StoresImportedKeysAndProfiles()
    {
        // A FAPI client authenticates with its keys (private_key_jwt or self_signed_tls_client_auth), so it is seeded
        // without a secret: its credentials are exactly the keys the client-key policy imported from its key set.
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Returns([])
            .Verifiable();

        var importedKey = new PersistedSecret
        {
            SecretId = "key-1",
            KeyId = "key-1",
            Use = null,
            Algorithm = "ES256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.MaxValue,
            SecretType = "ecc",
            SecretEncoding = "spki",
            KeySizeBits = 256,
            EncodedValue = "public-key",
        };
        const string jwks = """{"keys":[{"kty":"EC","kid":"key-1"}]}""";
        var mockKeyImporter = CreateStrictMock<IClientKeyImporter>();
        IReadOnlyList<PersistedSecret>? importedKeys = [importedKey];
        string? importError = null;
        mockKeyImporter
            .Setup(x =>
                x.TryImportRegisteredKeySet(
                    It.Is<JsonElement>(keySet => keySet.GetRawText() == jwks),
                    It.IsAny<DateTimeOffset>(),
                    out importedKeys,
                    out importError
                )
            )
            .Returns(true)
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "fapi1",
                    Jwks = jwks,
                    RedirectUris = ["https://client.example/callback"],
                    TokenEndpointAuthMethod = "private_key_jwt",
                    SecurityProfiles = ["fapi1-advanced"],
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object,
            mockKeyImporter.Object,
            CreateRedirectUriPolicy(isPermitted: true)
        );

        PersistedClient? storedClient = null;
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("fapi1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();
        mockClientStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedClient>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedClient client, CancellationToken _) => storedClient = client)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
            CancellationToken.None
        );

        Assert.NotNull(storedClient);
        Assert.Equal([importedKey], storedClient.Secrets.Value);
        var settingsDocument = storedClient.Settings.Value;
        Assert.Equal(
            "private_key_jwt",
            settingsDocument.GetProperty("token_endpoint_auth_method").GetString()
        );
        Assert.Equal(["fapi1-advanced"], GetStrings(settingsDocument, "security_profiles"));
    }

    [Fact]
    public async Task HandleAsync_WhenKeySetIsRefused_ThrowsNamingTheClient()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Returns([])
            .Verifiable();

        var mockKeyImporter = CreateStrictMock<IClientKeyImporter>();
        IReadOnlyList<PersistedSecret>? importedKeys = null;
        string? importError = "The key 'key-1' is a private key.";
        mockKeyImporter
            .Setup(x =>
                x.TryImportRegisteredKeySet(
                    It.IsAny<JsonElement>(),
                    It.IsAny<DateTimeOffset>(),
                    out importedKeys,
                    out importError
                )
            )
            .Returns(false)
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "fapi1",
                    Jwks = """{"keys":[{"kty":"EC","kid":"key-1","d":"private"}]}""",
                    RedirectUris = ["https://client.example/callback"],
                    TokenEndpointAuthMethod = "private_key_jwt",
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object,
            mockKeyImporter.Object,
            CreateRedirectUriPolicy(isPermitted: true)
        );

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("fapi1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'fapi1'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("is a private key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenARedirectUriKindIsNotPermitted_ThrowsNamingTheClient()
    {
        const string redirectUri = "http://client.example/callback";

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x => x.ValidateSettings(It.IsAny<JsonElement>(), "client", jsonOptions))
            .Returns([])
            .Verifiable();

        var options = new ConformanceSeedOptions
        {
            TenantId = WorkloadTenantId,
            Clients =
            [
                new ConformanceClientSeed
                {
                    ClientId = "client",
                    ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0LWNsaWVudA",
                    RedirectUris = [redirectUri],
                },
            ],
            TestUserPassword = null,
        };
        var handler = CreateHandler(
            options,
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object,
            redirectUriPolicy: CreateRedirectUriPolicy(isPermitted: false)
        );

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'client'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(redirectUri, exception.Message, StringComparison.Ordinal);
    }

    private static IEnumerable<string?> GetStrings(JsonElement document, string propertyName) =>
        document.GetProperty(propertyName).EnumerateArray().Select(element => element.GetString());

    #endregion
}
