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
using NCode.Identity.OpenId.Authentication.Clients.Coherence;
using NCode.Identity.OpenId.Core.Tests.Settings;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Coherence;

public class RegisteredValueCoherenceRuleTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockEffectiveSettings { get; }

    public RegisteredValueCoherenceRuleTests()
    {
        MockEffectiveSettings = CreateLooseMock<IReadOnlySettingCollection>();
    }

    #region Helpers

    private static SettingDescriptor<IReadOnlyCollection<string>> CreateIntersectList() =>
        new()
        {
            Name = OpenIdSettingNames.GrantTypesSupported,
            AppliesTo = [OpenIdSettingScopes.Client],
            OnMerge = SettingMerge.Intersect,
        };

    private static SettingDescriptor<IReadOnlyCollection<string>> CreateReplaceList() =>
        new()
        {
            Name = OpenIdSettingNames.RedirectUris,
            AppliesTo = [OpenIdSettingScopes.Client],
            OnMerge = SettingMerge.Replace,
        };

    private static SettingDescriptor<bool> CreateAndFlag() =>
        new()
        {
            Name = OpenIdSettingNames.TlsClientCertificateBoundAccessTokens,
            AppliesTo = [OpenIdSettingScopes.Client],
            OnMerge = SettingMerge.And,
        };

    private static SettingDescriptor<bool> CreateOrFlag() =>
        new()
        {
            Name = OpenIdSettingNames.IdTokenEncryptionRequired,
            AppliesTo = [OpenIdSettingScopes.Client],
            OnMerge = SettingMerge.Or,
        };

    private IReadOnlySettingCollection CreateRegisteredSettings(params Setting[] settings)
    {
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetEnumerator())
            .Returns(() => settings.AsEnumerable().GetEnumerator());
        return mockSettings.Object;
    }

    private void SetupEffectiveList(string settingName, params string[] values)
    {
        object? output = values;
        MockEffectiveSettings.Setup(x => x.TryGetValue(settingName, out output)).Returns(true);
    }

    private void SetupEffectiveFlag(string settingName, bool value)
    {
        object? output = value;
        MockEffectiveSettings.Setup(x => x.TryGetValue(settingName, out output)).Returns(true);
    }

    private RegisteredValueCoherenceRule CreateRule(params SettingDescriptor[] descriptors)
    {
        var mockProvider = CreateLooseMock<ISettingDescriptorCollectionProvider>();
        mockProvider
            .SetupGet(x => x.Collection)
            .Returns(new TestSettingDescriptorCollection(descriptors));
        return new RegisteredValueCoherenceRule(mockProvider.Object);
    }

    private ClientCoherenceContext CreateContext(IReadOnlySettingCollection registeredSettings) =>
        new()
        {
            RegisteredSettings = registeredSettings,
            EffectiveSettings = MockEffectiveSettings.Object,
        };

    #endregion

    #region SettingNames Property Tests

    [Fact]
    public void SettingNames_ListsEveryListAndFlagThatMergesWithALatticeOperation()
    {
        var rule = CreateRule(
            CreateIntersectList(),
            CreateAndFlag(),
            CreateReplaceList(),
            new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TokenEndpointAuthMethod,
                AppliesTo = [OpenIdSettingScopes.Client],
                OnMerge = SettingMerge.Min,
            }
        );

        var names = rule.SettingNames;

        Assert.Equal(2, names.Count);
        Assert.Contains(OpenIdSettingNames.GrantTypesSupported, names);
        Assert.Contains(OpenIdSettingNames.TlsClientCertificateBoundAccessTokens, names);
    }

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenARegisteredListValueIsNotEffective_ReportsIt()
    {
        var rule = CreateRule();
        var registered = CreateRegisteredSettings(
            CreateIntersectList().Create(["authorization_code", "implicit"])
        );
        SetupEffectiveList(OpenIdSettingNames.GrantTypesSupported, "authorization_code");

        var violations = rule.FindViolations(CreateContext(registered));

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.GrantTypesSupported,
                "implicit",
                OpenIdSettingNames.GrantTypesSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenEveryRegisteredListValueIsEffective_ReturnsEmpty()
    {
        var rule = CreateRule();
        var registered = CreateRegisteredSettings(
            CreateIntersectList().Create(["authorization_code", "implicit"])
        );
        SetupEffectiveList(
            OpenIdSettingNames.GrantTypesSupported,
            "implicit",
            "authorization_code",
            "refresh_token"
        );

        var violations = rule.FindViolations(CreateContext(registered));

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenAListMergesByProjection_ReturnsEmpty()
    {
        var rule = CreateRule();
        var registered = CreateRegisteredSettings(
            CreateReplaceList().Create(["https://a.example/cb", "https://b.example/cb"])
        );
        SetupEffectiveList(OpenIdSettingNames.RedirectUris, "https://a.example/cb");

        var violations = rule.FindViolations(CreateContext(registered));

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenARegisteredTrueFlagIsEffectivelyFalse_ReportsIt()
    {
        var rule = CreateRule();
        var registered = CreateRegisteredSettings(CreateAndFlag().Create(true));
        SetupEffectiveFlag(OpenIdSettingNames.TlsClientCertificateBoundAccessTokens, false);

        var violations = rule.FindViolations(CreateContext(registered));

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.TlsClientCertificateBoundAccessTokens,
                "true",
                OpenIdSettingNames.TlsClientCertificateBoundAccessTokens
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenARegisteredFalseFlagIsEffectivelyTrue_ReturnsEmpty()
    {
        var rule = CreateRule();
        var registered = CreateRegisteredSettings(CreateOrFlag().Create(false));
        SetupEffectiveFlag(OpenIdSettingNames.IdTokenEncryptionRequired, true);

        var violations = rule.FindViolations(CreateContext(registered));

        Assert.Empty(violations);
    }

    #endregion
}
