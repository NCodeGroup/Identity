// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class SeededClientValidatorTests : BaseTests
{
    private const string TenantId = "default";

    private JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<IOpenIdEnvironmentProvider> MockEnvironmentProvider { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IClientCoherenceValidator> MockClientCoherenceValidator { get; }

    private SeededClientValidator Validator { get; }

    public SeededClientValidatorTests()
    {
        MockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        MockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockClientCoherenceValidator = CreateStrictMock<IClientCoherenceValidator>();

        Validator = new SeededClientValidator(
            MockSettingSerializer.Object,
            MockEnvironmentProvider.Object,
            MockEffectiveSettingsResolver.Object,
            MockClientCoherenceValidator.Object
        );
    }

    private static PersistedTenant CreateTenant() =>
        new PersistedTenantBuilder { TenantId = TenantId }.Build();

    private void SetupEnvironment()
    {
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(JsonOptions);
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();
    }

    private void SetupValidateSettings(params SettingError[] settingErrors) =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(It.IsAny<JsonElement>(), OpenIdSettingScopes.Client, JsonOptions)
            )
            .Returns(settingErrors)
            .Verifiable();

    private IReadOnlySettingCollection SetupTenantResolution()
    {
        var platform = CreateLooseMock<ISettingCollection>().Object;
        var tenantSettings = CreateLooseMock<ISettingCollection>().Object;

        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(platform)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.Resolve(platform, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
            .Returns(tenantSettings)
            .Verifiable();

        return tenantSettings;
    }

    #region EnsureCanBeSeededAsync Tests

    [Fact]
    public async Task EnsureCanBeSeededAsync_WhenTheSettingsCannotBeHonored_ThrowsNamingTheClient()
    {
        SetupEnvironment();
        SetupValidateSettings(
            new SettingError(
                "grant_types_supported",
                "The setting 'grant_types_supported' must list only grant types."
            )
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Validator.EnsureCanBeSeededAsync(
                "developer client",
                "spa",
                CreateTenant(),
                JsonElement.Parse("{}"),
                CancellationToken.None
            )
        );

        Assert.Contains(
            "The developer client 'spa' cannot be seeded",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "The setting 'grant_types_supported' must list only grant types.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task EnsureCanBeSeededAsync_WhenARuleFindsAViolation_ThrowsWithItsDescription()
    {
        SetupEnvironment();
        SetupValidateSettings();
        var tenantSettings = SetupTenantResolution();

        var violation = new ClientCoherenceViolation(
            "redirect_uris",
            "http://localhost/callback",
            "redirect_uri_kinds_supported"
        );
        MockClientCoherenceValidator
            .Setup(x => x.Validate(tenantSettings, It.IsAny<JsonElement>()))
            .Returns([violation])
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Validator.EnsureCanBeSeededAsync(
                "developer client",
                "spa",
                CreateTenant(),
                JsonElement.Parse("{}"),
                CancellationToken.None
            )
        );

        Assert.Contains(
            "The developer client 'spa' cannot be seeded",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(violation.Description, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureCanBeSeededAsync_WhenTheClientIsCoherent_Completes()
    {
        SetupEnvironment();
        SetupValidateSettings();
        var tenantSettings = SetupTenantResolution();

        MockClientCoherenceValidator
            .Setup(x => x.Validate(tenantSettings, It.IsAny<JsonElement>()))
            .Returns([])
            .Verifiable();

        await Validator.EnsureCanBeSeededAsync(
            "developer client",
            "spa",
            CreateTenant(),
            JsonElement.Parse("{}"),
            CancellationToken.None
        );
    }

    #endregion
}
