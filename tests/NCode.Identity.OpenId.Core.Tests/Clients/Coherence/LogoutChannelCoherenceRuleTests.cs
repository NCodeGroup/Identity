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

public class LogoutChannelCoherenceRuleTests : BaseTests
{
    private const string BackChannelUri = "https://rp.example/backchannel-logout";
    private const string FrontChannelUri = "https://rp.example/frontchannel-logout";

    private Mock<IReadOnlySettingCollection> MockRegistered { get; }
    private Mock<IReadOnlySettingCollection> MockEffective { get; }

    public LogoutChannelCoherenceRuleTests()
    {
        MockRegistered = CreateLooseMock<IReadOnlySettingCollection>();
        MockEffective = CreateLooseMock<IReadOnlySettingCollection>();
    }

    #region Helpers

    private void SetupRegistered(SettingKey<string> key, string value)
    {
        string? output = value;
        MockRegistered.Setup(x => x.TryGetValue(key, out output)).Returns(true);
    }

    private void SetupRegistered(SettingKey<bool> key, bool value)
    {
        var output = value;
        MockRegistered.Setup(x => x.TryGetValue(key, out output)).Returns(true);
    }

    private void SetupEffective(SettingKey<bool> key, bool value)
    {
        var output = value;
        MockEffective.Setup(x => x.TryGetValue(key, out output)).Returns(true);
    }

    private IReadOnlyList<ClientCoherenceViolation> FindViolations() =>
        new LogoutChannelCoherenceRule().FindViolations(
            new ClientCoherenceContext
            {
                RegisteredSettings = MockRegistered.Object,
                EffectiveSettings = MockEffective.Object,
            }
        );

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenABackChannelUriIsUnsupported_ReportsTheUri()
    {
        SetupRegistered(OpenIdSettingKeys.BackChannelLogoutUri, BackChannelUri);
        SetupEffective(OpenIdSettingKeys.BackChannelLogoutSupported, false);

        var violation = Assert.Single(FindViolations());

        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.BackChannelLogoutUri,
                BackChannelUri,
                OpenIdSettingNames.BackChannelLogoutSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenABackChannelUriIsSupported_ReturnsEmpty()
    {
        SetupRegistered(OpenIdSettingKeys.BackChannelLogoutUri, BackChannelUri);
        SetupEffective(OpenIdSettingKeys.BackChannelLogoutSupported, true);

        Assert.Empty(FindViolations());
    }

    [Fact]
    public void FindViolations_WhenABackChannelSessionIsRequiredButUnsupported_ReportsTheRequirement()
    {
        SetupRegistered(OpenIdSettingKeys.BackChannelLogoutSessionRequired, true);
        SetupEffective(OpenIdSettingKeys.BackChannelLogoutSessionSupported, false);

        var violation = Assert.Single(FindViolations());

        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.BackChannelLogoutSessionRequired,
                "true",
                OpenIdSettingNames.BackChannelLogoutSessionSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenABackChannelSessionIsNotRequired_ReturnsEmpty()
    {
        SetupRegistered(OpenIdSettingKeys.BackChannelLogoutSessionRequired, false);
        SetupEffective(OpenIdSettingKeys.BackChannelLogoutSessionSupported, false);

        Assert.Empty(FindViolations());
    }

    [Fact]
    public void FindViolations_WhenAFrontChannelUriIsUnsupported_ReportsTheUri()
    {
        SetupRegistered(OpenIdSettingKeys.FrontChannelLogoutUri, FrontChannelUri);
        SetupEffective(OpenIdSettingKeys.FrontChannelLogoutSupported, false);

        var violation = Assert.Single(FindViolations());

        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.FrontChannelLogoutUri,
                FrontChannelUri,
                OpenIdSettingNames.FrontChannelLogoutSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenAFrontChannelSessionIsRequiredButUnsupported_ReportsTheRequirement()
    {
        SetupRegistered(OpenIdSettingKeys.FrontChannelLogoutSessionRequired, true);
        SetupEffective(OpenIdSettingKeys.FrontChannelLogoutSessionSupported, false);

        var violation = Assert.Single(FindViolations());

        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.FrontChannelLogoutSessionRequired,
                "true",
                OpenIdSettingNames.FrontChannelLogoutSessionSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenNothingIsRegistered_ReturnsEmpty()
    {
        SetupEffective(OpenIdSettingKeys.BackChannelLogoutSupported, false);
        SetupEffective(OpenIdSettingKeys.FrontChannelLogoutSupported, false);

        Assert.Empty(FindViolations());
    }

    #endregion
}
