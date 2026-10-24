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
