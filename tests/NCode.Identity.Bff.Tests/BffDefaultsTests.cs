// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Bff.Tests;

public class BffDefaultsTests : BaseTests
{
    [Fact]
    public void BffDefaults_CookieNames_UseTheHostHttpPrefix()
    {
        Assert.StartsWith("__Host-Http-", BffDefaults.SessionCookieName, StringComparison.Ordinal);
        Assert.StartsWith("__Host-Http-", BffDefaults.LoginCookieName, StringComparison.Ordinal);
        Assert.StartsWith(
            "__Host-Http-",
            BffDefaults.PageReloadCookieName,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void PageReloadCookieName_Value_IsTheReloadMarkerName()
    {
        Assert.Equal("__Host-Http-bff-reload", BffDefaults.PageReloadCookieName);
    }
}
