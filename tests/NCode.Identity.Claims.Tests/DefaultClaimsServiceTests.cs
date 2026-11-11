// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.Claims;

public class DefaultClaimsServiceTests
{
    private DefaultClaimsService Service { get; } = new();

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims));

    #region CopyClaims Tests

    [Fact]
    public void CopyClaims_CopiesOnlyRequestedClaimTypes()
    {
        var source = CreatePrincipal(
            new Claim("name", "alice"),
            new Claim("role", "admin"),
            new Claim("email", "a@b.c")
        );
        var target = new List<Claim>();

        Service.CopyClaims(source, target, preventDuplicates: false, "name", "role");

        Assert.Equal(2, target.Count);
        Assert.Contains(target, x => x is { Type: "name", Value: "alice" });
        Assert.Contains(target, x => x is { Type: "role", Value: "admin" });
    }

    [Fact]
    public void CopyClaims_WhenNoTypesMatch_CopiesNothing()
    {
        var source = CreatePrincipal(new Claim("name", "alice"));
        var target = new List<Claim>();

        Service.CopyClaims(source, target, preventDuplicates: false, "missing");

        Assert.Empty(target);
    }

    [Fact]
    public void CopyClaims_WhenPreventDuplicates_SkipsExistingTypes()
    {
        var source = CreatePrincipal(new Claim("name", "alice"), new Claim("name", "second"));
        var target = new List<Claim> { new("name", "existing") };

        Service.CopyClaims(source, target, preventDuplicates: true, "name");

        Assert.Single(target);
        Assert.Equal("existing", target[0].Value);
    }

    [Fact]
    public void CopyClaims_WhenAllowDuplicates_CopiesAll()
    {
        var source = CreatePrincipal(new Claim("name", "alice"), new Claim("name", "second"));
        var target = new List<Claim>();

        Service.CopyClaims(source, target, preventDuplicates: false, "name");

        Assert.Equal(2, target.Count);
    }

    #endregion
}
