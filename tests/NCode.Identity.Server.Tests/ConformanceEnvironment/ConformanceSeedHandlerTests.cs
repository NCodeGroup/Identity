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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
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
        IOpenIdEnvironmentProvider openIdEnvironmentProvider
    )
    {
        // Strict mocks: on the guard paths under test the secret, crypto, and time dependencies are untouched.
        var mockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        var mockCryptoService = CreateStrictMock<ICryptoService>();

        return new ConformanceSeedHandler(
            options,
            mockSecretGenerator.Object,
            mockCryptoService.Object,
            TimeProvider.System,
            settingSerializer,
            openIdEnvironmentProvider,
            NullLogger<ConformanceSeedHandler>.Instance
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
            Settings = null!,
            Secrets = null!,
        };

        return new SeedTenantCommand(
            new TenantSeedContext
            {
                Tenant = persistedTenant,
                Plane = plane,
                IsNewlyProvisioned = true,
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
    public async Task HandleAsync_WhenRootPlane_DoesNothing()
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

    private static IEnumerable<string?> GetStrings(JsonElement document, string propertyName) =>
        document.GetProperty(propertyName).EnumerateArray().Select(element => element.GetString());

    #endregion
}
