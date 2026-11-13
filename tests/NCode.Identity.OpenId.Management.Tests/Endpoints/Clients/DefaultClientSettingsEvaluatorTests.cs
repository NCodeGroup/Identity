// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public sealed class DefaultClientSettingsEvaluatorTests
{
    private const string TenantId = "tenant-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IClientCoherenceValidator> MockClientCoherenceValidator { get; }
    private Mock<IClientSecretIssuer> MockClientSecretIssuer { get; }
    private DefaultClientSettingsEvaluator Evaluator { get; }

    public DefaultClientSettingsEvaluatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockClientCoherenceValidator = MockRepository.Create<IClientCoherenceValidator>();
        MockClientSecretIssuer = MockRepository.Create<IClientSecretIssuer>();

        Evaluator = new DefaultClientSettingsEvaluator(
            MockEffectiveSettingsResolver.Object,
            MockClientCoherenceValidator.Object,
            MockClientSecretIssuer.Object
        );
    }

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new JsonObject());

    private static PersistedTenant CreateTenant() =>
        new PersistedTenantBuilder
        {
            TenantId = TenantId,
            ConcurrencyToken = "tenant-ct",
            SettingsConcurrencyToken = "tenant-settings-ct",
        }.Build();

    #region IsClientCredentialsOnly Tests

    [Fact]
    public void IsClientCredentialsOnly_WhenTheGrantTypesAreClientCredentialsAlone_ReturnsTrue()
    {
        var settings = JsonElement.Parse("""{"grant_types_supported":["client_credentials"]}""");

        var result = DefaultClientSettingsEvaluator.IsClientCredentialsOnly(settings);

        Assert.True(result);
    }

    [Fact]
    public void IsClientCredentialsOnly_WhenAnotherGrantTypeIsListed_ReturnsFalse()
    {
        var settings = JsonElement.Parse(
            """{"grant_types_supported":["client_credentials","refresh_token"]}"""
        );

        var result = DefaultClientSettingsEvaluator.IsClientCredentialsOnly(settings);

        Assert.False(result);
    }

    [Fact]
    public void IsClientCredentialsOnly_WhenTheSettingIsAbsent_ReturnsFalse()
    {
        var settings = JsonElement.Parse("{}");

        var result = DefaultClientSettingsEvaluator.IsClientCredentialsOnly(settings);

        Assert.False(result);
    }

    #endregion

    #region NameTokenEndpointAuthMethod Tests

    [Fact]
    public void NameTokenEndpointAuthMethod_WhenTheSettingsNameNone_NamesTheResolvedMethod()
    {
        var settings = JsonSerializer.SerializeToElement(
            new JsonObject { [OpenIdSettingNames.ClientName] = "client" }
        );

        var named = Evaluator.NameTokenEndpointAuthMethod(
            settings,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt
        );

        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
            named.GetProperty(OpenIdSettingNames.TokenEndpointAuthMethod).GetString()
        );
        Assert.Equal("client", named.GetProperty(OpenIdSettingNames.ClientName).GetString());
    }

    [Fact]
    public void NameTokenEndpointAuthMethod_WhenTheSettingsNameOne_KeepsIt()
    {
        var settings = JsonSerializer.SerializeToElement(
            new JsonObject
            {
                [OpenIdSettingNames.TokenEndpointAuthMethod] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .PrivateKeyJwt,
            }
        );

        var named = Evaluator.NameTokenEndpointAuthMethod(
            settings,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        );

        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            named.GetProperty(OpenIdSettingNames.TokenEndpointAuthMethod).GetString()
        );
    }

    [Fact]
    public void NameTokenEndpointAuthMethod_WhenTheMethodCannotBeResolved_KeepsTheSettings()
    {
        var settings = EmptyObject();

        var named = Evaluator.NameTokenEndpointAuthMethod(settings, null);

        Assert.False(named.TryGetProperty(OpenIdSettingNames.TokenEndpointAuthMethod, out _));
    }

    #endregion

    #region ValidateEffectiveSettingsAsync Tests

    [Fact]
    public async Task ValidateEffectiveSettingsAsync_WhenARuleFindsAViolation_KeysTheErrorByTheRegisteredSetting()
    {
        const string redirectUri = "myapp://callback";
        SetupTenantEffectiveSettings(
            new ClientCoherenceViolation(
                OpenIdSettingNames.RedirectUris,
                redirectUri,
                OpenIdSettingNames.RedirectUriKindsSupported
            )
        );

        var errors = await Evaluator.ValidateEffectiveSettingsAsync(
            TenantId,
            EmptyObject(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        var error = Assert.Single(errors);
        Assert.Equal(OpenIdSettingNames.RedirectUris, error.SettingName);
        Assert.Contains(redirectUri, error.Message, StringComparison.Ordinal);
        MockRepository.VerifyAll();
    }

    [Fact]
    public async Task ValidateEffectiveSettingsAsync_WhenTheTenantDoesNotExist_ReturnsNoErrors()
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var errors = await Evaluator.ValidateEffectiveSettingsAsync(
            TenantId,
            EmptyObject(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Empty(errors);
        MockRepository.VerifyAll();
    }

    private void SetupTenantEffectiveSettings(params ClientCoherenceViolation[] violations)
    {
        var teamSettings = MockRepository.Create<IReadOnlySettingCollection>().Object;
        var tenantSettings = MockRepository.Create<IReadOnlySettingCollection>().Object;

        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant())
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamSettings)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    teamSettings,
                    OpenIdSettingScopes.Tenant,
                    TenantId,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(tenantSettings)
            .Verifiable();
        MockClientCoherenceValidator
            .Setup(x => x.Validate(tenantSettings, It.IsAny<JsonElement>()))
            .Returns(violations)
            .Verifiable();
    }

    #endregion
}
