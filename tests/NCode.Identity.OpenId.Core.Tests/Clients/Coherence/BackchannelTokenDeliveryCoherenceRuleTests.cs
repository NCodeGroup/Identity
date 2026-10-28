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

public class BackchannelTokenDeliveryCoherenceRuleTests : BaseTests
{
    private const string NotificationEndpoint = "https://client.example/notify";

    private Mock<IReadOnlySettingCollection> MockSettings { get; }

    public BackchannelTokenDeliveryCoherenceRuleTests()
    {
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
    }

    #region Helpers

    private void SetupMode(string mode)
    {
        string? output = mode;
        MockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.BackchannelTokenDeliveryMode, out output))
            .Returns(true);
    }

    private void SetupEndpoint(string endpoint)
    {
        string? output = endpoint;
        MockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.ClientNotificationEndpoint, out output))
            .Returns(true);
    }

    private void SetupValues(SettingKey<IReadOnlyCollection<string>> key, params string[] values)
    {
        IReadOnlyCollection<string>? output = values;
        MockSettings.Setup(x => x.TryGetValue(key, out output)).Returns(true);
    }

    private static ClientCoherenceContext CreateContext(
        IReadOnlySettingCollection effectiveSettings
    ) => new() { RegisteredSettings = effectiveSettings, EffectiveSettings = effectiveSettings };

    #endregion

    #region FindViolations Tests

    [Theory]
    [InlineData(OpenIdProtocol.BackchannelTokenDeliveryModes.Ping)]
    [InlineData(OpenIdProtocol.BackchannelTokenDeliveryModes.Push)]
    public void FindViolations_WhenANotifiedModeHasNoNotificationEndpoint_ReportsTheMissingEndpoint(
        string mode
    )
    {
        SetupMode(mode);

        var violations = new BackchannelTokenDeliveryCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(
            ClientCoherenceViolation.Missing(
                OpenIdSettingNames.ClientNotificationEndpoint,
                OpenIdSettingNames.BackchannelTokenDeliveryMode,
                mode
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenACibaClientsModeIsNotSupported_ReportsTheMode()
    {
        SetupMode(OpenIdProtocol.BackchannelTokenDeliveryModes.Push);
        SetupEndpoint(NotificationEndpoint);
        SetupValues(OpenIdSettingKeys.GrantTypesSupported, OpenIdProtocol.GrantTypes.Ciba);
        SetupValues(
            OpenIdSettingKeys.BackchannelTokenDeliveryModesSupported,
            OpenIdProtocol.BackchannelTokenDeliveryModes.Poll,
            OpenIdProtocol.BackchannelTokenDeliveryModes.Ping
        );

        var violations = new BackchannelTokenDeliveryCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.BackchannelTokenDeliveryMode,
                OpenIdProtocol.BackchannelTokenDeliveryModes.Push,
                OpenIdSettingNames.BackchannelTokenDeliveryModesSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenTheClientCannotUseCiba_IgnoresTheSupportedModes()
    {
        SetupValues(
            OpenIdSettingKeys.GrantTypesSupported,
            OpenIdProtocol.GrantTypes.AuthorizationCode
        );
        SetupValues(
            OpenIdSettingKeys.BackchannelTokenDeliveryModesSupported,
            OpenIdProtocol.BackchannelTokenDeliveryModes.Ping
        );

        var violations = new BackchannelTokenDeliveryCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenThePollModeIsSupported_ReturnsEmpty()
    {
        SetupMode(OpenIdProtocol.BackchannelTokenDeliveryModes.Poll);
        SetupValues(OpenIdSettingKeys.GrantTypesSupported, OpenIdProtocol.GrantTypes.Ciba);
        SetupValues(
            OpenIdSettingKeys.BackchannelTokenDeliveryModesSupported,
            OpenIdProtocol.BackchannelTokenDeliveryModes.Poll,
            OpenIdProtocol.BackchannelTokenDeliveryModes.Ping
        );

        var violations = new BackchannelTokenDeliveryCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    #endregion
}
