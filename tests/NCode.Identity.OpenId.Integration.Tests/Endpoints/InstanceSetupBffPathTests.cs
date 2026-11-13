// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff;
using NCode.Identity.OpenId.Setup;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Provides tests proving that the BFF paths the instance setup registers for the management console's client match
/// the paths the BFF maps.
/// </summary>
public class InstanceSetupBffPathTests
{
    #region BffCallbackPath Tests

    [Fact]
    public void BffCallbackPath_WhenRead_MatchesTheBffCallbackRoute()
    {
        Assert.Equal(
            BffDefaults.EndpointPrefix + BffEndpointPaths.Callback,
            DefaultInstanceSetupService.BffCallbackPath
        );
    }

    #endregion

    #region BffBackChannelLogoutPath Tests

    [Fact]
    public void BffBackChannelLogoutPath_WhenRead_MatchesTheBffBackChannelLogoutRoute()
    {
        Assert.Equal(
            BffDefaults.EndpointPrefix + BffEndpointPaths.BackChannelLogout,
            DefaultInstanceSetupService.BffBackChannelLogoutPath
        );
    }

    #endregion
}
