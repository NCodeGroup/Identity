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
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Coherence;

public class RefreshTokenRotationRetryCoherenceRuleTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; }

    public RefreshTokenRotationRetryCoherenceRuleTests()
    {
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
    }

    #region Helpers

    private void SetupSettings(
        bool retryRequired,
        string policy,
        TimeSpan interval,
        string authMethod
    )
    {
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationRetryRequired))
            .Returns(retryRequired);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(policy);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenReuseInterval))
            .Returns(interval);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns(authMethod);
    }

    private static ClientCoherenceContext CreateContext(
        IReadOnlySettingCollection effectiveSettings
    ) => new() { RegisteredSettings = effectiveSettings, EffectiveSettings = effectiveSettings };

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenARetryIsRequiredAndEveryTokenRotatesWithoutAnInterval_ReportsTheInterval()
    {
        SetupSettings(
            retryRequired: true,
            policy: OpenIdConstants.RefreshTokenRotationPolicy.Always,
            interval: TimeSpan.Zero,
            authMethod: OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt
        );

        var violations = new RefreshTokenRotationRetryCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(OpenIdSettingNames.RefreshTokenReuseInterval, violation.SettingName);
        Assert.Equal("00:00:00", violation.Value);
        Assert.Equal(
            OpenIdSettingNames.RefreshTokenRotationRetryRequired,
            violation.GoverningSettingName
        );
    }

    [Fact]
    public void FindViolations_WhenARetryIsRequiredAndEveryTokenRotatesWithAnInterval_ReturnsEmpty()
    {
        SetupSettings(
            retryRequired: true,
            policy: OpenIdConstants.RefreshTokenRotationPolicy.Always,
            interval: TimeSpan.FromSeconds(30),
            authMethod: OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt
        );

        var violations = new RefreshTokenRotationRetryCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenARetryIsRequiredAndAPublicClientIsUnboundWithoutAnInterval_ReportsTheInterval()
    {
        SetupSettings(
            retryRequired: true,
            policy: OpenIdConstants.RefreshTokenRotationPolicy.Unbound,
            interval: TimeSpan.Zero,
            authMethod: OpenIdProtocol.ClientAuthenticationMethods.None
        );

        var violations = new RefreshTokenRotationRetryCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(OpenIdSettingNames.RefreshTokenReuseInterval, violation.SettingName);
        Assert.Equal("00:00:00", violation.Value);
        Assert.Equal(
            OpenIdSettingNames.RefreshTokenRotationRetryRequired,
            violation.GoverningSettingName
        );
    }

    [Fact]
    public void FindViolations_WhenARetryIsRequiredAndAConfidentialClientIsUnbound_ReturnsEmpty()
    {
        SetupSettings(
            retryRequired: true,
            policy: OpenIdConstants.RefreshTokenRotationPolicy.Unbound,
            interval: TimeSpan.Zero,
            authMethod: OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt
        );

        var violations = new RefreshTokenRotationRetryCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenNoRetryIsRequired_ReturnsEmpty()
    {
        SetupSettings(
            retryRequired: false,
            policy: OpenIdConstants.RefreshTokenRotationPolicy.Always,
            interval: TimeSpan.Zero,
            authMethod: OpenIdProtocol.ClientAuthenticationMethods.None
        );

        var violations = new RefreshTokenRotationRetryCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    #endregion
}
