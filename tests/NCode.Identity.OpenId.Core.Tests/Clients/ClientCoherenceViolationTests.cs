// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class ClientCoherenceViolationTests : BaseTests
{
    #region Description Property Tests

    [Fact]
    public void Description_ByDefault_SaysTheValueIsNotPermitted()
    {
        var violation = new ClientCoherenceViolation(
            "redirect_uris",
            "myapp://cb",
            "redirect_uri_kinds_supported"
        );

        Assert.Equal(
            "The 'redirect_uris' value 'myapp://cb' is not permitted by the effective 'redirect_uri_kinds_supported'.",
            violation.Description
        );
    }

    #endregion

    #region Missing Tests

    [Fact]
    public void Missing_DescribesTheRequiredSetting()
    {
        var violation = ClientCoherenceViolation.Missing(
            "client_notification_endpoint",
            "backchannel_token_delivery_mode",
            "ping"
        );

        Assert.Equal("client_notification_endpoint", violation.SettingName);
        Assert.Equal(string.Empty, violation.Value);
        Assert.Equal("backchannel_token_delivery_mode", violation.GoverningSettingName);
        Assert.Equal(
            "The effective 'backchannel_token_delivery_mode' value 'ping' requires a 'client_notification_endpoint' value.",
            violation.Description
        );
    }

    [Fact]
    public void Missing_EqualsAnotherWithTheSameArguments()
    {
        var first = ClientCoherenceViolation.Missing(
            "client_notification_endpoint",
            "backchannel_token_delivery_mode",
            "ping"
        );
        var second = ClientCoherenceViolation.Missing(
            "client_notification_endpoint",
            "backchannel_token_delivery_mode",
            "ping"
        );

        Assert.Equal(first, second);
    }

    #endregion
}
