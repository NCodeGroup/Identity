#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

namespace NCode.Identity.Claims;

public class DefaultClaimsServiceTests
{
    private readonly DefaultClaimsService _service = new();

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

        _service.CopyClaims(source, target, preventDuplicates: false, "name", "role");

        Assert.Equal(2, target.Count);
        Assert.Contains(target, x => x is { Type: "name", Value: "alice" });
        Assert.Contains(target, x => x is { Type: "role", Value: "admin" });
    }

    [Fact]
    public void CopyClaims_WhenNoTypesMatch_CopiesNothing()
    {
        var source = CreatePrincipal(new Claim("name", "alice"));
        var target = new List<Claim>();

        _service.CopyClaims(source, target, preventDuplicates: false, "missing");

        Assert.Empty(target);
    }

    [Fact]
    public void CopyClaims_WhenPreventDuplicates_SkipsExistingTypes()
    {
        var source = CreatePrincipal(new Claim("name", "alice"), new Claim("name", "second"));
        var target = new List<Claim> { new("name", "existing") };

        _service.CopyClaims(source, target, preventDuplicates: true, "name");

        Assert.Single(target);
        Assert.Equal("existing", target[0].Value);
    }

    [Fact]
    public void CopyClaims_WhenAllowDuplicates_CopiesAll()
    {
        var source = CreatePrincipal(new Claim("name", "alice"), new Claim("name", "second"));
        var target = new List<Claim>();

        _service.CopyClaims(source, target, preventDuplicates: false, "name");

        Assert.Equal(2, target.Count);
    }

    #endregion
}
