// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
