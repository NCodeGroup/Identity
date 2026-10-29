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

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose;
using Xunit;

namespace NCode.Identity.Bff.Tests;

public class BffSessionClaimContextTests : BaseTests
{
    private static BffSessionClaimContext CreateContext() =>
        new(
            new DefaultHttpContext(),
            "https://op.example",
            "user-1",
            [
                new Claim(JoseClaimNames.Payload.Sub, "user-1"),
                new Claim(JoseClaimNames.Payload.Acr, "urn:mace:incommon:iap:silver"),
                new Claim("email", "user@example.com"),
            ]
        );

    #region AddClaim Tests

    [Fact]
    public void AddClaim_WithAnOrdinaryType_AddsIt()
    {
        var context = CreateContext();

        context.AddClaim(new Claim("role", "admin"));

        Assert.Equal("admin", Assert.Single(context.Claims, claim => claim.Type == "role").Value);
    }

    [Theory]
    [InlineData("iss")]
    [InlineData("sub")]
    [InlineData("sid")]
    [InlineData("auth_time")]
    [InlineData("acr")]
    [InlineData("amr")]
    public void AddClaim_WithAReservedType_Throws(string claimType)
    {
        var context = CreateContext();

        var exception = Assert.Throws<ArgumentException>(() =>
            context.AddClaim(new Claim(claimType, "forged"))
        );

        Assert.Equal("claim", exception.ParamName);
        Assert.DoesNotContain(context.Claims, claim => claim.Value == "forged");
    }

    #endregion

    #region RemoveClaims Tests

    [Fact]
    public void RemoveClaims_WithAnOrdinaryType_RemovesEveryClaimOfIt()
    {
        var context = CreateContext();

        var removed = context.RemoveClaims("email");

        Assert.Equal(1, removed);
        Assert.DoesNotContain(context.Claims, claim => claim.Type == "email");
    }

    [Fact]
    public void RemoveClaims_WithAReservedType_ThrowsAndKeepsTheClaims()
    {
        var context = CreateContext();

        var exception = Assert.Throws<ArgumentException>(() =>
            context.RemoveClaims(JoseClaimNames.Payload.Acr)
        );

        Assert.Equal("claimType", exception.ParamName);
        Assert.Contains(context.Claims, claim => claim.Type == JoseClaimNames.Payload.Acr);
    }

    #endregion
}
