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
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultClientCoherenceValidatorTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; }
    private Mock<IReadOnlySettingCollection> MockPreviousSettings { get; }
    private Mock<IReadOnlySettingCollection> MockCurrentSettings { get; }
    private Mock<IOpenIdEnvironmentProvider> MockEnvironmentProvider { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISettingCollectionFactory> MockSettingCollectionFactory { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }

    public DefaultClientCoherenceValidatorTests()
    {
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockPreviousSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockCurrentSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        MockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        MockSettingCollectionFactory = CreateStrictMock<ISettingCollectionFactory>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
    }

    #region Helpers

    private Mock<IClientCoherenceRule> CreateRule(params string[] settingNames)
    {
        var mockRule = CreateStrictMock<IClientCoherenceRule>();
        mockRule.Setup(x => x.SettingNames).Returns(settingNames).Verifiable();
        return mockRule;
    }

    private static void SetupValue(
        Mock<IReadOnlySettingCollection> mockSettings,
        string settingName,
        object value
    )
    {
        object? output = value;
        mockSettings.Setup(x => x.TryGetValue(settingName, out output)).Returns(true);
    }

    private void SetupResolution(
        IReadOnlySettingCollection tenantSettings,
        ISettingCollection registeredSettings,
        IReadOnlySettingCollection effectiveSettings
    )
    {
        var jsonOptions = new JsonSerializerOptions();
        IReadOnlyCollection<Setting> deserialized = [];

        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.SetupGet(x => x.JsonSerializerOptions).Returns(jsonOptions).Verifiable();
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();

        MockSettingSerializer
            .Setup(x =>
                x.DeserializeSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Client,
                    jsonOptions
                )
            )
            .Returns(deserialized)
            .Verifiable();

        MockSettingCollectionFactory
            .Setup(x => x.Create(deserialized))
            .Returns(registeredSettings)
            .Verifiable();

        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(tenantSettings, OpenIdSettingScopes.Client, It.IsAny<JsonElement>())
            )
            .Returns(effectiveSettings)
            .Verifiable();
    }

    private DefaultClientCoherenceValidator CreateValidator(
        params Mock<IClientCoherenceRule>[] mockRules
    ) =>
        new(
            mockRules.Select(mockRule => mockRule.Object),
            MockEnvironmentProvider.Object,
            MockSettingSerializer.Object,
            MockSettingCollectionFactory.Object,
            MockEffectiveSettingsResolver.Object
        );

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WhenGivenADocument_RunsEveryRuleOverTheResolvedSettings()
    {
        var tenantSettings = CreateLooseMock<IReadOnlySettingCollection>().Object;
        var registeredSettings = CreateLooseMock<ISettingCollection>().Object;
        SetupResolution(tenantSettings, registeredSettings, MockSettings.Object);

        var violation = new ClientCoherenceViolation(
            "first_setting",
            "first_value",
            "first_ceiling"
        );

        var mockRule = CreateRule("first_setting");
        mockRule
            .Setup(x =>
                x.FindViolations(
                    It.Is<ClientCoherenceContext>(context =>
                        context.RegisteredSettings == registeredSettings
                        && context.EffectiveSettings == MockSettings.Object
                    )
                )
            )
            .Returns(new[] { violation })
            .Verifiable();

        var violations = CreateValidator(mockRule)
            .Validate(tenantSettings, JsonElement.Parse("{}"));

        Assert.Equal([violation], violations);
    }

    [Fact]
    public void Validate_WhenTheResolvedSettingsAreCoherent_ReturnsEmpty()
    {
        var tenantSettings = CreateLooseMock<IReadOnlySettingCollection>().Object;
        var registeredSettings = CreateLooseMock<ISettingCollection>().Object;
        SetupResolution(tenantSettings, registeredSettings, MockSettings.Object);

        var mockRule = CreateRule("first_setting");
        mockRule
            .Setup(x =>
                x.FindViolations(
                    It.Is<ClientCoherenceContext>(context =>
                        context.RegisteredSettings == registeredSettings
                        && context.EffectiveSettings == MockSettings.Object
                    )
                )
            )
            .Returns(Array.Empty<ClientCoherenceViolation>())
            .Verifiable();

        var violations = CreateValidator(mockRule)
            .Validate(tenantSettings, JsonElement.Parse("{}"));

        Assert.Empty(violations);
    }

    #endregion

    #region ReadsChangedSetting Tests

    [Fact]
    public void ReadsChangedSetting_WhenAListARuleReadsChanges_ReturnsTrue()
    {
        SetupValue(MockPreviousSettings, "list_setting", new[] { "PS256" });
        SetupValue(MockCurrentSettings, "list_setting", new[] { "PS256", "ES256" });

        var changed = CreateValidator(CreateRule("list_setting"))
            .ReadsChangedSetting(MockPreviousSettings.Object, MockCurrentSettings.Object);

        Assert.True(changed);
    }

    [Fact]
    public void ReadsChangedSetting_WhenAValueARuleReadsChanges_ReturnsTrue()
    {
        SetupValue(MockPreviousSettings, "value_setting", "RS256");
        SetupValue(MockCurrentSettings, "value_setting", "PS256");

        var changed = CreateValidator(CreateRule("value_setting"))
            .ReadsChangedSetting(MockPreviousSettings.Object, MockCurrentSettings.Object);

        Assert.True(changed);
    }

    [Fact]
    public void ReadsChangedSetting_WhenASettingARuleReadsIsRemoved_ReturnsTrue()
    {
        SetupValue(MockPreviousSettings, "value_setting", "RSA-OAEP-256");

        var changed = CreateValidator(CreateRule("value_setting"))
            .ReadsChangedSetting(MockPreviousSettings.Object, MockCurrentSettings.Object);

        Assert.True(changed);
    }

    [Fact]
    public void ReadsChangedSetting_WhenOnlyTheOrderOfAListChanges_ReturnsFalse()
    {
        SetupValue(MockPreviousSettings, "list_setting", new[] { "PS256", "ES256" });
        SetupValue(MockCurrentSettings, "list_setting", new[] { "ES256", "PS256" });

        var changed = CreateValidator(CreateRule("list_setting"))
            .ReadsChangedSetting(MockPreviousSettings.Object, MockCurrentSettings.Object);

        Assert.False(changed);
    }

    [Fact]
    public void ReadsChangedSetting_WhenOnlyASettingNoRuleReadsChanges_ReturnsFalse()
    {
        SetupValue(MockPreviousSettings, "value_setting", "PS256");
        SetupValue(MockCurrentSettings, "value_setting", "PS256");
        SetupValue(MockPreviousSettings, "list_setting", new[] { "PS256" });
        SetupValue(MockCurrentSettings, "list_setting", new[] { "PS256" });
        SetupValue(MockPreviousSettings, "unread_setting", "urn:acr:one");
        SetupValue(MockCurrentSettings, "unread_setting", "urn:acr:two");

        var changed = CreateValidator(CreateRule("value_setting", "list_setting"))
            .ReadsChangedSetting(MockPreviousSettings.Object, MockCurrentSettings.Object);

        Assert.False(changed);
    }

    #endregion
}
