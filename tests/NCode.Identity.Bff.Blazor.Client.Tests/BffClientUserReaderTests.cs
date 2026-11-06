// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Client.Tests;

public class BffClientUserReaderTests : BaseTests
{
    #region TryRead Tests

    [Fact]
    public void TryRead_FullDocument_BuildsPrincipal()
    {
        var document = JsonElement.Parse(
            """
            {
              "sub": "user-1",
              "name": "Ada",
              "claims": { "email": "ada@example.com", "age": 36 },
              "session": { "expiresAt": 1700003600, "idleExpiresAt": 1700000600 },
              "logoutUrl": "/bff/logout",
              "logoutSid": "guard-1",
              "extra": { "ignored": true }
            }
            """
        );

        var result = BffClientUserReader.TryRead(document, out var user);

        Assert.True(result);
        Assert.NotNull(user);
        var identity = Assert.IsType<ClaimsIdentity>(user.Principal.Identity);
        Assert.Equal(BffBlazorClientDefaults.AuthenticationType, identity.AuthenticationType);
        Assert.True(identity.IsAuthenticated);
        Assert.Equal("Ada", identity.Name);
        Assert.Equal("user-1", user.Principal.FindFirst("sub")?.Value);
        Assert.Equal("ada@example.com", user.Principal.FindFirst("email")?.Value);
        Assert.Equal("36", user.Principal.FindFirst("age")?.Value);
        Assert.Equal("/bff/logout", user.Principal.FindFirst(BffClaimTypes.LogoutUrl)?.Value);
        Assert.Equal("guard-1", user.Principal.FindFirst(BffClaimTypes.LogoutSid)?.Value);
        Assert.Equal(
            new DateTimeOffset(2023, 11, 14, 22, 23, 20, TimeSpan.Zero),
            user.IdleExpiresAt
        );
    }

    [Fact]
    public void TryRead_ArrayClaims_AddsOneClaimPerElement()
    {
        var document = JsonElement.Parse(
            """
            {
              "sub": "user-1",
              "claims": { "role": ["admin", "editor", 7] }
            }
            """
        );

        var result = BffClientUserReader.TryRead(document, out var user);

        Assert.True(result);
        Assert.NotNull(user);
        Assert.Equal(
            ["admin", "editor", "7"],
            user.Principal.FindAll("role").Select(claim => claim.Value)
        );
        Assert.True(user.Principal.IsInRole("admin"));
    }

    [Fact]
    public void TryRead_MissingSub_ReturnsFalse()
    {
        var document = JsonElement.Parse("""{ "name": "Ada" }""");

        var result = BffClientUserReader.TryRead(document, out var user);

        Assert.False(result);
        Assert.Null(user);
    }

    [Fact]
    public void TryRead_NumericSub_ReturnsFalse()
    {
        var document = JsonElement.Parse("""{ "sub": 42 }""");

        var result = BffClientUserReader.TryRead(document, out var user);

        Assert.False(result);
        Assert.Null(user);
    }

    [Fact]
    public void TryRead_FractionalIdleExpiry_ReadsInstant()
    {
        var document = JsonElement.Parse(
            """{ "sub": "user-1", "session": { "idleExpiresAt": 1700000600.5 } }"""
        );

        var result = BffClientUserReader.TryRead(document, out var user);

        Assert.True(result);
        Assert.NotNull(user);
        Assert.Equal(
            new DateTimeOffset(2023, 11, 14, 22, 23, 20, 500, TimeSpan.Zero),
            user.IdleExpiresAt
        );
    }

    [Fact]
    public void TryRead_NotAnObject_ReturnsFalse()
    {
        var document = JsonElement.Parse("""["user-1"]""");

        var result = BffClientUserReader.TryRead(document, out var user);

        Assert.False(result);
        Assert.Null(user);
    }

    [Fact]
    public void TryRead_MissingSession_HasNoIdleExpiry()
    {
        var document = JsonElement.Parse("""{ "sub": "user-1" }""");

        var result = BffClientUserReader.TryRead(document, out var user);

        Assert.True(result);
        Assert.NotNull(user);
        Assert.Null(user.IdleExpiresAt);
    }

    #endregion
}
