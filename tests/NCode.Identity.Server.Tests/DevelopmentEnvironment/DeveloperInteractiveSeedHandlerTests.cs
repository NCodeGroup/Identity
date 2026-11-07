// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Identity.Server.Logging;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DeveloperInteractiveSeedHandlerTests : BaseTests
{
    private const string WorkloadTenantId = "default";

    private DeveloperInteractiveSeedHandler CreateHandler(
        DeveloperInteractiveSeedOptions options,
        ILogger<DeveloperInteractiveSeedHandler>? logger = null
    ) =>
        new(
            options,
            CreateStrictMock<IClientKeyImporter>().Object,
            TimeProvider.System,
            CreateSeededClientValidator(),
            CreateSeededLocalAccountValidator(),
            logger ?? CreateStrictMock<ILogger<DeveloperInteractiveSeedHandler>>().Object
        );

    private DeveloperInteractiveSeedHandler CreateBffHandler(
        DeveloperInteractiveSeedOptions options,
        IClientKeyImporter clientKeyImporter,
        ILogger<DeveloperInteractiveSeedHandler>? logger = null
    ) =>
        new(
            options,
            clientKeyImporter,
            TimeProvider.System,
            CreateAcceptingSeededClientValidator(),
            CreateSeededLocalAccountValidator(),
            logger ?? CreateStrictMock<ILogger<DeveloperInteractiveSeedHandler>>().Object
        );

    private static bool MessageContains(object? state, string text) =>
        state?.ToString()?.Contains(text, StringComparison.Ordinal) == true;

    private ILogger<DeveloperInteractiveSeedHandler> CreateReplacedLogger()
    {
        var mockLogger = CreateStrictMock<ILogger<DeveloperInteractiveSeedHandler>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true).Verifiable();
#pragma warning disable CA1873 // The arguments are matchers of a mock setup, not log arguments.
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Information,
                    It.Is<EventId>(id => id.Id == EventIds.DeveloperBffClientReplaced),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();
#pragma warning restore CA1873
        return mockLogger.Object;
    }

    private IClientKeyImporter CreateKeyImporter(params PersistedSecret[] keys)
    {
        var mockKeyImporter = CreateStrictMock<IClientKeyImporter>();
        IReadOnlyList<PersistedSecret>? importedKeys = keys;
        string? importError = null;
        mockKeyImporter
            .Setup(x =>
                x.TryImportRegisteredKeySet(
                    It.IsAny<JsonElement>(),
                    It.IsAny<DateTimeOffset>(),
                    out importedKeys,
                    out importError
                )
            )
            .Returns(true)
            .Verifiable();
        return mockKeyImporter.Object;
    }

    private SeededClientValidator CreateAcceptingSeededClientValidator()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(It.IsAny<JsonElement>(), OpenIdSettingScopes.Client, jsonOptions)
            )
            .Returns([])
            .Verifiable();

        var platform = CreateLooseMock<ISettingCollection>().Object;
        var tenantSettings = CreateLooseMock<ISettingCollection>().Object;
        var mockResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        mockResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(platform)
            .Verifiable();
        mockResolver
            .Setup(x => x.Resolve(platform, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
            .Returns(tenantSettings)
            .Verifiable();

        var mockValidator = CreateStrictMock<IClientCoherenceValidator>();
        mockValidator
            .Setup(x => x.Validate(tenantSettings, It.IsAny<JsonElement>()))
            .Returns([])
            .Verifiable();

        return CreateSeededClientValidator(
            mockSettingSerializer.Object,
            mockEnvironmentProvider.Object,
            mockResolver.Object,
            mockValidator.Object
        );
    }

    private static PersistedSecret CreateImportedKey() =>
        new()
        {
            SecretId = "key-1",
            KeyId = "key-1",
            Use = null,
            Algorithm = "ES256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.MaxValue,
            ActivatesWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.MaxValue,
            SecretType = "ecc",
            SecretEncoding = "spki",
            Origin = SecretOrigins.Generated,
            KeySizeBits = 256,
            EncodedValue = "public-key",
        };

    private static DeveloperBffClientSeed CreateBffClient(string? jwks) =>
        new()
        {
            ClientId = "console",
            Jwks = jwks,
            RedirectUris = ["https://console.example/bff/callback"],
        };

    private IStoreManager CreateBffStoreManager(
        PersistedClient? existing,
        Action<PersistedClient>? onAdded = null
    )
    {
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("console", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .Verifiable();
        if (onAdded is not null)
        {
            mockClientStore
                .Setup(x => x.AddAsync(It.IsAny<PersistedClient>(), It.IsAny<CancellationToken>()))
                .Callback((PersistedClient client, CancellationToken _) => onAdded(client))
                .Returns(ValueTask.CompletedTask)
                .Verifiable();
        }

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        return mockStoreManager.Object;
    }

    private IStoreManager CreateReplacingStoreManager(
        PersistedClient existing,
        Action<PersistedClient> onAdded
    )
    {
        var sequence = new MockSequence();
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockClientStore
            .InSequence(sequence)
            .Setup(x => x.GetOrDefaultAsync("console", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .Verifiable();
        mockClientStore
            .InSequence(sequence)
            .Setup(x => x.RemoveAsync("console", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        mockClientStore
            .InSequence(sequence)
            .Setup(x => x.AddAsync(It.IsAny<PersistedClient>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedClient client, CancellationToken _) => onAdded(client))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();
        return mockStoreManager.Object;
    }

    private static PersistedClient CreateStoredBffClient(
        string redirectUri,
        params PersistedSecret[] keys
    ) =>
        new PersistedClientBuilder
        {
            TenantId = WorkloadTenantId,
            ClientId = "console",
            Settings = JsonElement.Parse(
                $$"""
                {
                  "require_dpop_bound_access_tokens": true,
                  "require_pushed_authorization_requests": true,
                  "grant_types_supported": ["authorization_code", "refresh_token"],
                  "token_endpoint_auth_method": "private_key_jwt",
                  "redirect_uris": ["{{redirectUri}}"]
                }
                """
            ),
            Secrets = keys,
        }.Build();

    private static IEnumerable<string?> GetStrings(JsonElement jsonElement, string propertyName) =>
        jsonElement
            .GetProperty(propertyName)
            .EnumerateArray()
            .Select(element => element.GetString());

    private SeededClientValidator CreateSeededClientValidator(
        ISettingSerializer? settingSerializer = null,
        IOpenIdEnvironmentProvider? openIdEnvironmentProvider = null,
        IEffectiveSettingsResolver? effectiveSettingsResolver = null,
        IClientCoherenceValidator? clientCoherenceValidator = null
    ) =>
        new(
            settingSerializer ?? CreateStrictMock<ISettingSerializer>().Object,
            openIdEnvironmentProvider ?? CreateStrictMock<IOpenIdEnvironmentProvider>().Object,
            effectiveSettingsResolver ?? CreateStrictMock<IEffectiveSettingsResolver>().Object,
            clientCoherenceValidator ?? CreateStrictMock<IClientCoherenceValidator>().Object
        );

    private SeededLocalAccountValidator CreateSeededLocalAccountValidator(
        IEffectiveSettingsResolver? effectiveSettingsResolver = null,
        IPasswordPolicy? passwordPolicy = null
    ) =>
        new(
            effectiveSettingsResolver ?? CreateStrictMock<IEffectiveSettingsResolver>().Object,
            passwordPolicy ?? CreateStrictMock<IPasswordPolicy>().Object
        );

    private SeedTenantCommand CreateCommand(string tenantId, TenantPlane plane) =>
        // Strict mock: the store manager is untouched on the guard paths under test.
        CreateCommand(tenantId, plane, CreateStrictMock<IStoreManager>().Object);

    private static SeedTenantCommand CreateCommand(
        string tenantId,
        TenantPlane plane,
        IStoreManager storeManager
    )
    {
        var persistedTenant = new PersistedTenant
        {
            TenantId = tenantId,
            TeamId = "platform",
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

    private static DeveloperSpaClientSeed CreateSpaClient() =>
        new() { ClientId = "spa", RedirectUris = ["https://spa.example/callback"] };

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNothingConfigured_DoesNothing()
    {
        // There are no built-in clients and no default password, so the default options seed nothing.
        var handler = CreateHandler(new DeveloperInteractiveSeedOptions());

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNoTenantIsNamed_DoesNothing()
    {
        var handler = CreateHandler(
            new DeveloperInteractiveSeedOptions
            {
                SpaClients = [CreateSpaClient()],
                AdminPassword = "dev-password",
            }
        );

        await handler.HandleAsync(CreateCommand("root", TenantPlane.Root), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantDoesNotMatchConfiguredTarget_DoesNothing()
    {
        var handler = CreateHandler(
            new DeveloperInteractiveSeedOptions
            {
                SpaClients = [CreateSpaClient()],
                AdminPassword = "dev-password",
                TenantId = "tenant-a",
            }
        );

        await handler.HandleAsync(
            CreateCommand("tenant-b", TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenSpaClientConfigured_RegistersItAsAPublicClient()
    {
        // A single-page application cannot keep a secret, so it registers 'none': its one method, and its type.
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        // The validation result stops the seeding before the client is stored, once the document is captured.
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

        var options = new DeveloperInteractiveSeedOptions
        {
            TenantId = WorkloadTenantId,
            SpaClients = [CreateSpaClient()],
            AdminPassword = null,
        };
        var handler = new DeveloperInteractiveSeedHandler(
            options,
            CreateStrictMock<IClientKeyImporter>().Object,
            TimeProvider.System,
            CreateSeededClientValidator(
                mockSettingSerializer.Object,
                mockEnvironmentProvider.Object
            ),
            CreateSeededLocalAccountValidator(),
            CreateStrictMock<ILogger<DeveloperInteractiveSeedHandler>>().Object
        );

        var mockClientStore = CreateStrictMock<IClientStore>();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("spa", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
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
            OpenIdProtocol.ClientAuthenticationMethods.None,
            settingsDocument.GetProperty("token_endpoint_auth_method").GetString()
        );
        Assert.False(
            settingsDocument.TryGetProperty("token_endpoint_auth_methods_supported", out _)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheSpaClientWouldBeIncoherent_ThrowsNamingTheClient()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(jsonOptions);
        var mockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        mockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        var mockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        mockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(It.IsAny<JsonElement>(), OpenIdSettingScopes.Client, jsonOptions)
            )
            .Returns([])
            .Verifiable();

        var platform = CreateLooseMock<ISettingCollection>().Object;
        var tenantSettings = CreateLooseMock<ISettingCollection>().Object;
        var mockResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        mockResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(platform)
            .Verifiable();
        mockResolver
            .Setup(x => x.Resolve(platform, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
            .Returns(tenantSettings)
            .Verifiable();

        var mockValidator = CreateStrictMock<IClientCoherenceValidator>();
        mockValidator
            .Setup(x => x.Validate(tenantSettings, It.IsAny<JsonElement>()))
            .Returns([
                new ClientCoherenceViolation(
                    OpenIdSettingNames.RedirectUris,
                    "https://spa.example/callback",
                    OpenIdSettingNames.RedirectUriKindsSupported
                ),
            ])
            .Verifiable();

        var options = new DeveloperInteractiveSeedOptions
        {
            TenantId = WorkloadTenantId,
            SpaClients = [CreateSpaClient()],
            AdminPassword = null,
        };
        var handler = new DeveloperInteractiveSeedHandler(
            options,
            CreateStrictMock<IClientKeyImporter>().Object,
            TimeProvider.System,
            CreateSeededClientValidator(
                mockSettingSerializer.Object,
                mockEnvironmentProvider.Object,
                mockResolver.Object,
                mockValidator.Object
            ),
            CreateSeededLocalAccountValidator(),
            CreateStrictMock<ILogger<DeveloperInteractiveSeedHandler>>().Object
        );

        // No AddAsync setup: the strict store refuses a client that would be stored.
        var mockClientStore = CreateStrictMock<IClientStore>();
        mockClientStore
            .Setup(x => x.GetOrDefaultAsync("spa", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(mockClientStore.Object)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'spa'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "https://spa.example/callback",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheAdministratorPasswordIsRefused_ThrowsNamingTheAdministrator()
    {
        var platform = CreateLooseMock<ISettingCollection>().Object;
        var tenantSettings = CreateLooseMock<ISettingCollection>().Object;
        var mockResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        mockResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(platform)
            .Verifiable();
        mockResolver
            .Setup(x => x.Resolve(platform, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
            .Returns(tenantSettings)
            .Verifiable();

        var mockPolicy = CreateStrictMock<IPasswordPolicy>();
        mockPolicy
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<PasswordPolicyContext>(),
                    It.IsAny<ReadOnlyMemory<char>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                PasswordPolicyResult.Rejected(
                    PasswordPolicyErrors.TooShort,
                    "The password must have at least 15 characters."
                )
            )
            .Verifiable();

        var options = new DeveloperInteractiveSeedOptions
        {
            TenantId = WorkloadTenantId,
            AdminPassword = "short",
        };
        var handler = new DeveloperInteractiveSeedHandler(
            options,
            CreateStrictMock<IClientKeyImporter>().Object,
            TimeProvider.System,
            CreateSeededClientValidator(),
            CreateSeededLocalAccountValidator(mockResolver.Object, mockPolicy.Object),
            CreateStrictMock<ILogger<DeveloperInteractiveSeedHandler>>().Object
        );

        // No AddAsync setup: the strict store refuses an account that would be stored.
        var mockAccountStore = CreateStrictMock<ILocalAccountStore>();
        mockAccountStore
            .Setup(x => x.GetByUserNameOrDefaultAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedLocalAccount?)null)
            .Verifiable();
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(mockAccountStore.Object)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
                CancellationToken.None
            )
        );

        Assert.Contains("'admin'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenBffClientConfigured_StoresItsSettingsAndImportedKeys()
    {
        const string jwks = """{"keys":[{"kty":"EC","kid":"key-1"}]}""";
        var importedKey = CreateImportedKey();
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

        var bffClient = CreateBffClient(jwks);
        bffClient.PostLogoutRedirectUris = ["https://console.example/"];
        bffClient.BackchannelLogoutUri = "https://console.example/bff/backchannel-logout";
        var handler = CreateBffHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [bffClient],
                AdminPassword = null,
            },
            mockKeyImporter.Object
        );

        PersistedClient? storedClient = null;
        var storeManager = CreateBffStoreManager(null, client => storedClient = client);

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, storeManager),
            CancellationToken.None
        );

        Assert.NotNull(storedClient);
        Assert.Equal("console", storedClient.ClientId);
        Assert.Equal(WorkloadTenantId, storedClient.TenantId);
        Assert.False(storedClient.IsDisabled);
        Assert.Equal([importedKey], storedClient.Secrets.Value);

        var settingsDocument = storedClient.Settings.Value;
        Assert.Equal(
            ["https://console.example/bff/callback"],
            GetStrings(settingsDocument, "redirect_uris")
        );
        Assert.Equal(
            ["https://console.example/"],
            GetStrings(settingsDocument, "post_logout_redirect_uris")
        );
        Assert.Equal(
            "https://console.example/bff/backchannel-logout",
            settingsDocument.GetProperty("backchannel_logout_uri").GetString()
        );
        Assert.True(
            settingsDocument.GetProperty("backchannel_logout_session_required").GetBoolean()
        );
        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            settingsDocument.GetProperty("token_endpoint_auth_method").GetString()
        );
        Assert.Equal(
            ["authorization_code", "refresh_token"],
            GetStrings(settingsDocument, "grant_types_supported")
        );
        Assert.True(
            settingsDocument.GetProperty("require_pushed_authorization_requests").GetBoolean()
        );
        Assert.True(settingsDocument.GetProperty("require_dpop_bound_access_tokens").GetBoolean());
    }

    [Fact]
    public async Task HandleAsync_WhenBffClientHasNoLogoutUris_OmitsTheirSettings()
    {
        var importedKey = CreateImportedKey();
        var mockKeyImporter = CreateStrictMock<IClientKeyImporter>();
        IReadOnlyList<PersistedSecret>? importedKeys = [importedKey];
        string? importError = null;
        mockKeyImporter
            .Setup(x =>
                x.TryImportRegisteredKeySet(
                    It.IsAny<JsonElement>(),
                    It.IsAny<DateTimeOffset>(),
                    out importedKeys,
                    out importError
                )
            )
            .Returns(true)
            .Verifiable();

        var handler = CreateBffHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [CreateBffClient("""{"keys":[]}""")],
                AdminPassword = null,
            },
            mockKeyImporter.Object
        );

        PersistedClient? storedClient = null;
        var storeManager = CreateBffStoreManager(null, client => storedClient = client);

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, storeManager),
            CancellationToken.None
        );

        Assert.NotNull(storedClient);
        var settingsDocument = storedClient.Settings.Value;
        Assert.False(settingsDocument.TryGetProperty("post_logout_redirect_uris", out _));
        Assert.False(settingsDocument.TryGetProperty("backchannel_logout_uri", out _));
        Assert.False(settingsDocument.TryGetProperty("backchannel_logout_session_required", out _));
    }

    [Fact]
    public async Task HandleAsync_WhenStoredBffClientMatchesItsConfiguration_LeavesItAlone()
    {
        const string redirectUri = "https://console.example/bff/callback";
        var configuredKey = CreateImportedKey();

        // The secret identifier is generated per import and the timestamps are not compared.
        var storedKey = configuredKey with
        {
            SecretId = "stored-secret-id",
            CreatedWhen = DateTimeOffset.UnixEpoch.AddDays(1),
        };
        var existing = CreateStoredBffClient(redirectUri, storedKey);

        // Strict store: neither RemoveAsync nor AddAsync is set up, so either call fails the test.
        var handler = CreateBffHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [CreateBffClient("""{"keys":[]}""")],
                AdminPassword = null,
            },
            CreateKeyImporter(configuredKey)
        );
        var storeManager = CreateBffStoreManager(existing);

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, storeManager),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenStoredBffClientHasADifferentKey_ReplacesItFromConfiguration()
    {
        var configuredKey = CreateImportedKey();
        var storedKey = configuredKey with { EncodedValue = "another-public-key" };
        var existing = CreateStoredBffClient("https://console.example/bff/callback", storedKey);

        var handler = CreateBffHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [CreateBffClient("""{"keys":[]}""")],
                AdminPassword = null,
            },
            CreateKeyImporter(configuredKey),
            CreateReplacedLogger()
        );

        PersistedClient? addedClient = null;
        var storeManager = CreateReplacingStoreManager(existing, client => addedClient = client);

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, storeManager),
            CancellationToken.None
        );

        Assert.NotNull(addedClient);
        Assert.Equal([configuredKey], addedClient.Secrets.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenStoredBffClientHasADifferentRedirectUri_ReplacesItFromConfiguration()
    {
        var configuredKey = CreateImportedKey();
        var existing = CreateStoredBffClient("https://other.example/bff/callback", configuredKey);

        var handler = CreateBffHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [CreateBffClient("""{"keys":[]}""")],
                AdminPassword = null,
            },
            CreateKeyImporter(configuredKey),
            CreateReplacedLogger()
        );

        PersistedClient? addedClient = null;
        var storeManager = CreateReplacingStoreManager(existing, client => addedClient = client);

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, storeManager),
            CancellationToken.None
        );

        Assert.NotNull(addedClient);
        Assert.Equal(
            ["https://console.example/bff/callback"],
            GetStrings(addedClient.Settings.Value, "redirect_uris")
        );
    }

    [Fact]
    public async Task HandleAsync_WhenStoredBffClientHasAnExtraKey_ReplacesItFromConfiguration()
    {
        var configuredKey = CreateImportedKey();
        var extraKey = configuredKey with { KeyId = "key-2", EncodedValue = "second-public-key" };
        var existing = CreateStoredBffClient(
            "https://console.example/bff/callback",
            configuredKey,
            extraKey
        );

        var handler = CreateBffHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [CreateBffClient("""{"keys":[]}""")],
                AdminPassword = null,
            },
            CreateKeyImporter(configuredKey),
            CreateReplacedLogger()
        );

        PersistedClient? addedClient = null;
        var storeManager = CreateReplacingStoreManager(existing, client => addedClient = client);

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, storeManager),
            CancellationToken.None
        );

        Assert.NotNull(addedClient);
        Assert.Equal([configuredKey], addedClient.Secrets.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenBffClientKeySetIsRefused_ThrowsNamingTheClient()
    {
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

        var handler = CreateBffHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [CreateBffClient("""{"keys":[{"kty":"EC","d":"private"}]}""")],
                AdminPassword = null,
            },
            mockKeyImporter.Object
        );

        // Strict store manager with no setups: the key set is refused before the store is reached.
        var storeManager = CreateStrictMock<IStoreManager>().Object;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, storeManager),
                CancellationToken.None
            )
        );

        Assert.Contains("'console'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("is a private key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenBffClientKeySetIsNotJson_ThrowsNamingTheClient()
    {
        // Strict key importer: an unparsable key set is refused before the importer is reached.
        var handler = CreateBffHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [CreateBffClient("not json")],
                AdminPassword = null,
            },
            CreateStrictMock<IClientKeyImporter>().Object
        );
        var storeManager = CreateStrictMock<IStoreManager>().Object;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.HandleAsync(
                CreateCommand(WorkloadTenantId, TenantPlane.Workload, storeManager),
                CancellationToken.None
            )
        );

        Assert.Contains("'console'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenBffClientSetsNothing_SkipsItSilently()
    {
        // Strict logger and store manager with no setups: an entry that sets nothing is neither logged nor stored.
        var handler = CreateHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [new DeveloperBffClientSeed()],
                AdminPassword = null,
            }
        );

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenBffClientLacksItsKeySet_SkipsItAndWarnsNamingTheKey()
    {
        var mockLogger = CreateStrictMock<ILogger<DeveloperInteractiveSeedHandler>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(id => id.Id == EventIds.DeveloperBffClientSkipped),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            MessageContains(state, "DeveloperInteractiveSeed:BffClients:1:Jwks")
                            && !MessageContains(state, ":RedirectUris")
                    ),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();

        // Strict store manager with no setups: an entry that does not seed never reaches the store.
        var handler = CreateHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [new DeveloperBffClientSeed(), CreateBffClient(null)],
                AdminPassword = null,
            },
            mockLogger.Object
        );

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenBffClientLacksRedirectUris_SkipsItAndWarnsNamingTheKey()
    {
        var mockLogger = CreateStrictMock<ILogger<DeveloperInteractiveSeedHandler>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(id => id.Id == EventIds.DeveloperBffClientSkipped),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            MessageContains(
                                state,
                                "DeveloperInteractiveSeed:BffClients:0:RedirectUris"
                            )
                    ),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();

        var bffClient = CreateBffClient("""{"keys":[]}""");
        bffClient.RedirectUris = [];
        var handler = CreateHandler(
            new DeveloperInteractiveSeedOptions
            {
                TenantId = WorkloadTenantId,
                BffClients = [bffClient],
                AdminPassword = null,
            },
            mockLogger.Object
        );

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    #endregion
}
