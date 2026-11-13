// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public sealed class DefaultClientCreatorTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IClientValidator> MockClientValidator { get; }
    private Mock<IClientSettingsEvaluator> MockClientSettingsEvaluator { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<IClientKeyImporter> MockClientKeyImporter { get; }
    private Mock<IClientKeySourceChecker> MockClientKeySourceChecker { get; }
    private DefaultClientCreator Creator { get; }

    public DefaultClientCreatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockClientStore = MockRepository.Create<IClientStore>();
        MockClientValidator = MockRepository.Create<IClientValidator>();
        MockClientSettingsEvaluator = MockRepository.Create<IClientSettingsEvaluator>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockClientKeyImporter = MockRepository.Create<IClientKeyImporter>();
        MockClientKeySourceChecker = MockRepository.Create<IClientKeySourceChecker>();

        var openIdEnvironmentProvider = Mock.Of<IOpenIdEnvironmentProvider>(provider =>
            provider.Get()
            == Mock.Of<OpenIdEnvironment>(environment =>
                environment.JsonSerializerOptions == JsonSerializerOptions.Default
            )
        );

        Creator = new DefaultClientCreator(
            MockClientValidator.Object,
            MockClientSettingsEvaluator.Object,
            MockSettingSerializer.Object,
            MockClientKeyImporter.Object,
            MockClientKeySourceChecker.Object,
            TimeProvider.System,
            openIdEnvironmentProvider
        );
    }

    #region Helpers

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new JsonObject());

    private static ClientCreationRequest CreateRequest(
        string clientId = ClientId,
        JsonElement? settings = null,
        string? owningNodeType = null,
        string? owningNodeId = null,
        JsonElement? jwks = null
    ) =>
        new()
        {
            TenantId = TenantId,
            ClientId = clientId,
            IsDisabled = false,
            OwningNodeType = owningNodeType,
            OwningNodeId = owningNodeId,
            Settings = settings ?? EmptyObject(),
            Jwks = jwks,
        };

    private void SetupValidateCreate(ManagementError? error) =>
        MockClientValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.Is<PersistedClient>(client =>
                        client.TenantId == TenantId && client.ClientId == ClientId
                    ),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    private void SetupValidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Client,
                    JsonSerializerOptions.Default
                )
            )
            .Returns([])
            .Verifiable();

    private void SetupNoEffectiveSettingErrors() =>
        MockClientSettingsEvaluator
            .Setup(x =>
                x.ValidateEffectiveSettingsAsync(
                    TenantId,
                    It.IsAny<JsonElement>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((IReadOnlyList<SettingError>)[])
            .Verifiable();

    private void SetupStagedAuthenticationMethod()
    {
        MockClientSettingsEvaluator
            .Setup(x =>
                x.GetTokenEndpointAuthMethodAsync(
                    TenantId,
                    ClientId,
                    It.IsAny<JsonElement>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic)
            .Verifiable();
        MockClientSettingsEvaluator
            .Setup(x =>
                x.NameTokenEndpointAuthMethod(
                    It.IsAny<JsonElement>(),
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
                )
            )
            .Returns(
                JsonSerializer.SerializeToElement(
                    new JsonObject
                    {
                        [OpenIdSettingNames.TokenEndpointAuthMethod] = OpenIdProtocol
                            .ClientAuthenticationMethods
                            .ClientSecretBasic,
                    }
                )
            )
            .Verifiable();
    }

    private void SetupAdd(Action<PersistedClient>? capture = null)
    {
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedClient>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedClient client, CancellationToken _) => capture?.Invoke(client))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    #endregion

    #region StageAsync Tests

    [Fact]
    public async Task StageAsync_ValidClient_AddsClientWithoutSaving()
    {
        SetupValidateCreate(error: null);
        SetupValidSettings();
        SetupNoEffectiveSettingErrors();
        SetupStagedAuthenticationMethod();
        PersistedClient? added = null;
        // Strict mocks: no SaveChangesAsync setup, so saving the store manager would fail the test.
        SetupAdd(client => added = client);

        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            CreateRequest(),
            CancellationToken.None
        );

        Assert.True(creation.Succeeded);
        Assert.Same(added, creation.Client);
        Assert.Null(creation.Error);
        Assert.Null(creation.SettingErrors);
        Assert.Equal(TenantId, creation.Client.TenantId);
        Assert.Equal(ClientId, creation.Client.ClientId);
        Assert.Equal(TenantId, creation.Client.Settings.TenantId);
        Assert.Equal(TenantId, creation.Client.Secrets.TenantId);
        Assert.Empty(creation.Client.Secrets.Value);
        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            creation
                .Client.Settings.Value.GetProperty(OpenIdSettingNames.TokenEndpointAuthMethod)
                .GetString()
        );
        MockRepository.VerifyAll();
    }

    [Theory]
    [InlineData("")]
    [InlineData("client id")]
    [InlineData("client/id")]
    [InlineData("client:id")]
    public async Task StageAsync_ClientIdInvalid_Returns400(string clientId)
    {
        // Strict mocks: no validator, evaluator or store setup, so reaching any of them would fail the test.
        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            CreateRequest(clientId),
            CancellationToken.None
        );

        Assert.False(creation.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, creation.Error?.StatusCode);
        Assert.Null(creation.SettingErrors);
    }

    [Fact]
    public async Task StageAsync_ClientIdTooLong_Returns400()
    {
        var clientId = new string('a', 256);

        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            CreateRequest(clientId),
            CancellationToken.None
        );

        Assert.False(creation.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, creation.Error?.StatusCode);
    }

    [Fact]
    public async Task StageAsync_ClientIdTaken_Returns409()
    {
        SetupValidateCreate(
            new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "The client already exists.",
            }
        );

        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            CreateRequest(),
            CancellationToken.None
        );

        Assert.False(creation.Succeeded);
        Assert.Equal(StatusCodes.Status409Conflict, creation.Error?.StatusCode);
        MockRepository.VerifyAll();
    }

    [Fact]
    public async Task StageAsync_MachineClientNotClientCredentialsOnly_ReturnsInvalidSettings()
    {
        SetupValidateCreate(error: null);
        var settings = JsonSerializer.SerializeToElement(
            new JsonObject
            {
                [OpenIdSettingNames.GrantTypesSupported] = new JsonArray(
                    OpenIdProtocol.GrantTypes.ClientCredentials,
                    OpenIdProtocol.GrantTypes.RefreshToken
                ),
            }
        );

        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            CreateRequest(
                settings: settings,
                owningNodeType: ResourceNodeTypes.Team,
                owningNodeId: "team-1"
            ),
            CancellationToken.None
        );

        Assert.False(creation.Succeeded);
        Assert.Null(creation.Error);
        var messages = Assert.Single(
            creation.SettingErrors ?? throw new InvalidOperationException(),
            pair => pair.Key == OpenIdSettingNames.GrantTypesSupported
        );
        Assert.Equal(
            [DefaultClientSettingsEvaluator.MachineClientGrantTypesDetail],
            messages.Value
        );
        MockRepository.VerifyAll();
    }

    [Fact]
    public async Task StageAsync_JwksWithJwksUri_Returns400()
    {
        SetupValidateCreate(error: null);
        SetupValidSettings();
        SetupNoEffectiveSettingErrors();
        MockClientKeySourceChecker
            .Setup(x => x.HasJwksUri(It.IsAny<JsonElement>()))
            .Returns(true)
            .Verifiable();

        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            CreateRequest(jwks: JsonElement.Parse("""{"keys":[]}""")),
            CancellationToken.None
        );

        Assert.False(creation.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, creation.Error?.StatusCode);
        MockRepository.VerifyAll();
    }

    [Fact]
    public async Task StageAsync_JwksInvalid_Returns400()
    {
        SetupValidateCreate(error: null);
        SetupValidSettings();
        SetupNoEffectiveSettingErrors();
        MockClientKeySourceChecker
            .Setup(x => x.HasJwksUri(It.IsAny<JsonElement>()))
            .Returns(false)
            .Verifiable();
        IReadOnlyList<PersistedSecret>? secrets = null;
        string? importError = "The key set is not valid.";
        MockClientKeyImporter
            .Setup(x =>
                x.TryImportRegisteredKeySet(
                    It.IsAny<JsonElement>(),
                    It.IsAny<DateTimeOffset>(),
                    out secrets,
                    out importError
                )
            )
            .Returns(false)
            .Verifiable();

        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            CreateRequest(jwks: JsonElement.Parse("""{"keys":[]}""")),
            CancellationToken.None
        );

        Assert.False(creation.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, creation.Error?.StatusCode);
        Assert.Equal("The key set is not valid.", creation.Error?.Detail);
        MockRepository.VerifyAll();
    }

    [Fact]
    public async Task StageAsync_Jwks_ImportsKeys()
    {
        SetupValidateCreate(error: null);
        SetupValidSettings();
        SetupNoEffectiveSettingErrors();
        SetupStagedAuthenticationMethod();
        SetupAdd();
        MockClientKeySourceChecker
            .Setup(x => x.HasJwksUri(It.IsAny<JsonElement>()))
            .Returns(false)
            .Verifiable();
        IReadOnlyList<PersistedSecret>? importedSecrets = new List<PersistedSecret>();
        string? importError = null;
        MockClientKeyImporter
            .Setup(x =>
                x.TryImportRegisteredKeySet(
                    It.IsAny<JsonElement>(),
                    It.IsAny<DateTimeOffset>(),
                    out importedSecrets,
                    out importError
                )
            )
            .Returns(true)
            .Verifiable();

        var creation = await Creator.StageAsync(
            MockStoreManager.Object,
            CreateRequest(jwks: JsonElement.Parse("""{"keys":[]}""")),
            CancellationToken.None
        );

        Assert.True(creation.Succeeded);
        Assert.Same(importedSecrets, creation.Client.Secrets.Value);
        MockRepository.VerifyAll();
    }

    #endregion
}
