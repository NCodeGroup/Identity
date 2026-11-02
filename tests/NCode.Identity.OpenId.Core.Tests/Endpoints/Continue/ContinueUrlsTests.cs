// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Continue;

public class ContinueUrlsTests
{
    #region GetStateOrDefault Tests

    [Fact]
    public void GetStateOrDefault_WhenTheUrlCarriesAState_ReturnsIt()
    {
        var result = ContinueUrls.GetStateOrDefault("https://op.example/oauth2/continue?state=abc");

        Assert.Equal("abc", result);
    }

    [Fact]
    public void GetStateOrDefault_WhenTheUrlHasNoQuery_ReturnsNull()
    {
        var result = ContinueUrls.GetStateOrDefault("/account/sessions");

        Assert.Null(result);
    }

    [Fact]
    public void GetStateOrDefault_WhenTheQueryHasNoState_ReturnsNull()
    {
        var result = ContinueUrls.GetStateOrDefault("/x?y=1");

        Assert.Null(result);
    }

    #endregion
}
