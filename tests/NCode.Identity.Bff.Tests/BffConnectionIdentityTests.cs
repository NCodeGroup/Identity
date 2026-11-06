// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Xunit;

namespace NCode.Identity.Bff.Tests;

public class BffConnectionIdentityTests : BaseTests
{
    private static ClaimsIdentity CreateIdentity() =>
        new([new Claim("sub", "alice"), new Claim("role", "admin")], "bff", "name", "role");

    #region Constructor Tests

    [Fact]
    public void Constructor_CopiesClaimsAndAuthenticationType()
    {
        var session = CreateStrictMock<IBffConnectionSession>();

        var identity = new BffConnectionIdentity(CreateIdentity(), session.Object);

        Assert.Same(session.Object, identity.Session);
        Assert.Equal("bff", identity.AuthenticationType);
        Assert.Equal(2, identity.Claims.Count());
        Assert.Equal("alice", identity.FindFirst("sub")?.Value);
        Assert.Equal("admin", identity.FindFirst("role")?.Value);
    }

    [Fact]
    public void Constructor_WithNullIdentity_Throws()
    {
        var session = CreateStrictMock<IBffConnectionSession>();

        Assert.Throws<ArgumentNullException>(() =>
            new BffConnectionIdentity(null!, session.Object)
        );
    }

    [Fact]
    public void Constructor_WithNullSession_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BffConnectionIdentity(CreateIdentity(), null!)
        );
    }

    #endregion

    #region Clone Tests

    [Fact]
    public void Clone_ReturnsIdentityWithoutSession()
    {
        var session = CreateStrictMock<IBffConnectionSession>();
        var identity = new BffConnectionIdentity(CreateIdentity(), session.Object);

        var clone = identity.Clone();

        Assert.IsNotType<BffConnectionIdentity>(clone);
        Assert.Equal("bff", clone.AuthenticationType);
        Assert.Equal(
            identity.Claims.Select(x => (x.Type, x.Value)),
            clone.Claims.Select(x => (x.Type, x.Value))
        );
    }

    #endregion

    #region GetBffConnectionSession Tests

    [Fact]
    public void GetBffConnectionSession_WithConnectionIdentity_ReturnsSession()
    {
        var session = CreateStrictMock<IBffConnectionSession>();
        var principal = new ClaimsPrincipal([
            CreateIdentity(),
            new BffConnectionIdentity(CreateIdentity(), session.Object),
        ]);

        var result = principal.GetBffConnectionSession();

        Assert.Same(session.Object, result);
    }

    [Fact]
    public void GetBffConnectionSession_WithoutConnectionIdentity_ReturnsNull()
    {
        var principal = new ClaimsPrincipal(CreateIdentity());

        var result = principal.GetBffConnectionSession();

        Assert.Null(result);
    }

    #endregion
}
