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

using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class TenantAddressRulesTests
{
    #region IsValidTenantId Tests

    [Theory]
    [InlineData("a")]
    [InlineData("0")]
    [InlineData("acme")]
    [InlineData("acme-dev")]
    [InlineData("direct-supply-prod")]
    [InlineData("a1-b2-c3")]
    public void IsValidTenantId_WhenLowercaseHostLabel_ReturnsTrue(string tenantId)
    {
        Assert.True(TenantAddressRules.IsValidTenantId(tenantId));
    }

    [Fact]
    public void IsValidTenantId_WhenSixtyThreeCharacters_ReturnsTrue()
    {
        Assert.True(TenantAddressRules.IsValidTenantId(new string('a', 63)));
    }

    [Fact]
    public void IsValidTenantId_WhenSixtyFourCharacters_ReturnsFalse()
    {
        Assert.False(TenantAddressRules.IsValidTenantId(new string('a', 64)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Acme")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("ac_me")]
    [InlineData("ac.me")]
    [InlineData("ac me")]
    [InlineData("acme\n")]
    [InlineData("äcme")]
    public void IsValidTenantId_WhenNotALowercaseHostLabel_ReturnsFalse(string? tenantId)
    {
        Assert.False(TenantAddressRules.IsValidTenantId(tenantId));
    }

    #endregion

    #region TryParseIssuer Tests

    [Theory]
    [InlineData("https://op.example")]
    [InlineData("https://op.example/")]
    [InlineData("https://op.example:8443/acme")]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1/acme")]
    [InlineData("http://[::1]:5000")]
    public void TryParseIssuer_WhenValid_ReturnsTrue(string issuer)
    {
        var result = TenantAddressRules.TryParseIssuer(issuer, out var issuerUri);

        Assert.True(result);
        Assert.NotNull(issuerUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("op.example")]
    [InlineData("/acme")]
    [InlineData("ftp://op.example")]
    [InlineData("http://op.example")]
    [InlineData("https://user:pass@op.example")]
    [InlineData("https://op.example/?a=1")]
    [InlineData("https://op.example/?")]
    [InlineData("https://op.example/#a")]
    [InlineData("https://op.example/#")]
    [InlineData(" https://op.example")]
    [InlineData("https://op.example/a b")]
    public void TryParseIssuer_WhenInvalid_ReturnsFalse(string? issuer)
    {
        var result = TenantAddressRules.TryParseIssuer(issuer, out var issuerUri);

        Assert.False(result);
        Assert.Null(issuerUri);
    }

    [Fact]
    public void TryParseIssuer_WhenLongerThanTheMaximum_ReturnsFalse()
    {
        var issuer = "https://op.example/" + new string('a', 1000);

        Assert.False(TenantAddressRules.TryParseIssuer(issuer, out _));
    }

    #endregion

    #region IssuerEndsWithRoutePath Tests

    [Theory]
    [InlineData("https://op.example", "")]
    [InlineData("https://op.example", null)]
    [InlineData("https://op.example/acme", "/acme")]
    [InlineData("https://op.example/acme/", "/acme")]
    [InlineData("https://op.example/base/acme", "/acme")]
    [InlineData("https://op.example/tenants/acme", "/tenants/acme")]
    public void IssuerEndsWithRoutePath_WhenIssuerEndsWithRoute_ReturnsTrue(
        string issuer,
        string? routePath
    )
    {
        Assert.True(TenantAddressRules.IssuerEndsWithRoutePath(new Uri(issuer), routePath));
    }

    [Theory]
    [InlineData("https://op.example", "/acme")]
    [InlineData("https://op.example/xacme", "/acme")]
    [InlineData("https://op.example/acme/v2", "/acme")]
    [InlineData("https://op.example/globex", "/acme")]
    public void IssuerEndsWithRoutePath_WhenIssuerDoesNotEndWithRoute_ReturnsFalse(
        string issuer,
        string routePath
    )
    {
        Assert.False(TenantAddressRules.IssuerEndsWithRoutePath(new Uri(issuer), routePath));
    }

    #endregion
}
