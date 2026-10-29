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
using System.Text.Json;

namespace NCode.Identity.Jwt.Claims;

public class DefaultClaimsIdentityFactoryTests
{
    private static DefaultClaimsIdentityFactory Factory { get; } = new();

    #region CreateClaimsIdentity Tests

    [Fact]
    public void CreateClaimsIdentity_WithAClaimSet_TypesEachClaimByItsJsonValue()
    {
        var claimSet = JsonElement.Parse(
            """{"sub":"user-1","email_verified":true,"groups":["a","b"],"name":"Ada","role":"admin"}"""
        );

        var identity = Factory.CreateClaimsIdentity(claimSet, "UserInfo", "name", "role");

        Assert.Equal("UserInfo", identity.AuthenticationType);
        Assert.Equal("Ada", identity.Name);
        Assert.True(identity.HasClaim("role", "admin"));
        Assert.Equal(ClaimValueTypes.Boolean, identity.FindFirst("email_verified")?.ValueType);
        Assert.Equal(["a", "b"], identity.FindAll("groups").Select(claim => claim.Value));
    }

    [Fact]
    public void CreateClaimsIdentity_WithoutAnIssuerClaim_StampsTheSuppliedIssuer()
    {
        var claimSet = JsonElement.Parse("""{"sub":"user-1"}""");

        var identity = Factory.CreateClaimsIdentity(
            claimSet,
            "UserInfo",
            issuer: "https://op.example"
        );

        Assert.Equal("https://op.example", identity.FindFirst("sub")?.Issuer);
    }

    [Fact]
    public void CreateClaimsIdentity_WithAnIssuerClaim_PrefersIt()
    {
        var claimSet = JsonElement.Parse("""{"iss":"https://token.example","sub":"user-1"}""");

        var identity = Factory.CreateClaimsIdentity(claimSet, "Jwt", issuer: "https://op.example");

        Assert.Equal("https://token.example", identity.FindFirst("sub")?.Issuer);
    }

    [Fact]
    public void CreateClaimsIdentity_WithoutAnyIssuer_UsesTheDefaultIssuer()
    {
        var identity = Factory.CreateClaimsIdentity(JsonElement.Parse("""{"sub":"u"}"""), "Jwt");

        Assert.Equal(ClaimsIdentity.DefaultIssuer, identity.FindFirst("sub")?.Issuer);
        Assert.Equal(ClaimsIdentity.DefaultNameClaimType, identity.NameClaimType);
        Assert.Equal(ClaimsIdentity.DefaultRoleClaimType, identity.RoleClaimType);
    }

    [Fact]
    public void CreateClaimsIdentity_WithANonObject_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Factory.CreateClaimsIdentity(JsonElement.Parse("""["sub"]"""), "Jwt")
        );

        Assert.Equal("claimSet", exception.ParamName);
    }

    #endregion
}
