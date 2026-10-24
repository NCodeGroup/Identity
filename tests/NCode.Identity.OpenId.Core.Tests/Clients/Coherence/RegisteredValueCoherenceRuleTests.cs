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

    private static SettingDescriptor<TimeSpan> CreateMinLifetime() =>
        new()
        {
            Name = OpenIdSettingNames.AccessTokenLifetime,
            AppliesTo = [OpenIdSettingScopes.Client],
            OnMerge = SettingMerge.Min,
        };

    private static SettingDescriptor<int> CreateMaxLength() =>
        new()
        {
            Name = OpenIdSettingNames.UserCodeLength,
            AppliesTo = [OpenIdSettingScopes.Client],
            OnMerge = SettingMerge.Max,
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

    private void SetupEffectiveValue(string settingName, object value)
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

        var mockScopeProvider = CreateLooseMock<ISettingScopeCollectionProvider>();
        mockScopeProvider.SetupGet(x => x.Collection).Returns(TestSettingScopeCollection.OpenId);

        return new RegisteredValueCoherenceRule(mockProvider.Object, mockScopeProvider.Object);
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
    public void SettingNames_ListsEverySettingThatMergesWithAMeetWithinTheClientScope()
    {
        var rule = CreateRule(
            CreateIntersectList(),
            CreateAndFlag(),
            CreateMinLifetime(),
            CreateReplaceList(),
            CreateOrFlag(),
            CreateMaxLength(),
            new SettingDescriptor<TimeSpan>
            {
                Name = OpenIdSettingNames.SectorIdentifierUriFetchTimeout,
                AppliesTo = [OpenIdSettingScopes.Tenant],
                OnMerge = SettingMerge.Min,
            }
        );

        var names = rule.SettingNames;

        Assert.Equal(3, names.Count);
        Assert.Contains(OpenIdSettingNames.GrantTypesSupported, names);
        Assert.Contains(OpenIdSettingNames.TlsClientCertificateBoundAccessTokens, names);
        Assert.Contains(OpenIdSettingNames.AccessTokenLifetime, names);
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

    [Fact]
    public void FindViolations_WhenARegisteredValueExceedsItsMinCeiling_ReportsIt()
    {
        var rule = CreateRule();
        var registered = CreateRegisteredSettings(
            CreateMinLifetime().Create(TimeSpan.FromHours(1))
        );
        SetupEffectiveValue(OpenIdSettingNames.AccessTokenLifetime, TimeSpan.FromMinutes(5));

        var violations = rule.FindViolations(CreateContext(registered));

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.AccessTokenLifetime,
                "01:00:00",
                OpenIdSettingNames.AccessTokenLifetime
            )
            {
                Description =
                    "The 'access_token_lifetime' value '01:00:00' exceeds the effective 'access_token_lifetime' value '00:05:00'.",
            },
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenARegisteredValueEqualsItsMinCeiling_ReturnsEmpty()
    {
        var rule = CreateRule();
        var registered = CreateRegisteredSettings(
            CreateMinLifetime().Create(TimeSpan.FromMinutes(5))
        );
        SetupEffectiveValue(OpenIdSettingNames.AccessTokenLifetime, TimeSpan.FromMinutes(5));

        var violations = rule.FindViolations(CreateContext(registered));

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenAMaxFloorRaisesARegisteredValue_ReturnsEmpty()
    {
        var rule = CreateRule();
        var registered = CreateRegisteredSettings(CreateMaxLength().Create(6));
        SetupEffectiveValue(OpenIdSettingNames.UserCodeLength, 8);

        var violations = rule.FindViolations(CreateContext(registered));

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenAListOfAnotherElementTypeLosesAValue_ReportsIt()
    {
        const string Name = "it_ports_supported";

        var rule = CreateRule();
        var descriptor = new SettingDescriptor<IReadOnlyCollection<int>>
        {
            Name = Name,
            AppliesTo = [OpenIdSettingScopes.Client],
            OnMerge = SettingMerge.Intersect,
        };
        var registered = CreateRegisteredSettings(descriptor.Create([443, 8443]));
        SetupEffectiveValue(Name, new List<int> { 443 });

        var violations = rule.FindViolations(CreateContext(registered));

        var violation = Assert.Single(violations);
        Assert.Equal(new ClientCoherenceViolation(Name, "8443", Name), violation);
    }

    #endregion
}
