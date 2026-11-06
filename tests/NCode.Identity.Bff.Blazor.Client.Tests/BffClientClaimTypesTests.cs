// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Bff.Blazor.Client.Tests;

public class BffClientClaimTypesTests : BaseTests
{
    #region LogoutUrl

    [Fact]
    public void LogoutUrl_EqualsServerClaimType()
    {
        Assert.Equal(BffClaimTypes.LogoutUrl, BffClientClaimTypes.LogoutUrl);
    }

    #endregion

    #region LogoutSid

    [Fact]
    public void LogoutSid_EqualsServerClaimType()
    {
        Assert.Equal(BffClaimTypes.LogoutSid, BffClientClaimTypes.LogoutSid);
    }

    #endregion
}
