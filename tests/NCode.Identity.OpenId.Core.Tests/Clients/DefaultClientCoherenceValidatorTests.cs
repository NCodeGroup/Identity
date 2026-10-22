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

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultClientCoherenceValidatorTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; }
    private Mock<IReadOnlySettingCollection> MockPreviousSettings { get; }
    private Mock<IReadOnlySettingCollection> MockCurrentSettings { get; }

    public DefaultClientCoherenceValidatorTests()
    {
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockPreviousSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockCurrentSettings = CreateLooseMock<IReadOnlySettingCollection>();
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

    private static DefaultClientCoherenceValidator CreateValidator(
        params Mock<IClientCoherenceRule>[] mockRules
    ) => new(mockRules.Select(mockRule => mockRule.Object));

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_ReturnsTheViolationsOfEveryRule()
    {
        var first = new ClientCoherenceViolation("first_setting", "first_value", "first_ceiling");
        var second = new ClientCoherenceViolation(
            "second_setting",
            "second_value",
            "second_ceiling"
        );
        var third = new ClientCoherenceViolation("third_setting", "third_value", "third_ceiling");

        var mockFirstRule = CreateRule("first_setting");
        mockFirstRule
            .Setup(x => x.FindViolations(MockSettings.Object))
            .Returns(new[] { first, second })
            .Verifiable();

        var mockSecondRule = CreateRule("third_setting");
        mockSecondRule
            .Setup(x => x.FindViolations(MockSettings.Object))
            .Returns(new[] { third })
            .Verifiable();

        var violations = CreateValidator(mockFirstRule, mockSecondRule)
            .Validate(MockSettings.Object);

        Assert.Equal([first, second, third], violations);
    }

    [Fact]
    public void Validate_WhenNoRuleFindsAViolation_ReturnsEmpty()
    {
        var mockRule = CreateRule("first_setting");
        mockRule
            .Setup(x => x.FindViolations(MockSettings.Object))
            .Returns(Array.Empty<ClientCoherenceViolation>())
            .Verifiable();

        var violations = CreateValidator(mockRule).Validate(MockSettings.Object);

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
