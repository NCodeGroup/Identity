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

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

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
        new()
        {
            TenantId = TenantId,
            Issuer = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = TenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = TenantId,
                Value = JsonElement.Parse("{}"),
            },
            Secrets = null!,
        };

    private void SetupEnvironment()
    {
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(_jsonOptions);
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();
    }

    private void SetupValidateSettings(params SettingError[] settingErrors) =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Client,
                    _jsonOptions
                )
            )
            .Returns(settingErrors)
            .Verifiable();

    private IReadOnlySettingCollection SetupTenantResolution()
    {
        var server = CreateLooseMock<ISettingCollection>().Object;
        var tenantSettings = CreateLooseMock<ISettingCollection>().Object;

        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(server)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.Resolve(server, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
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
