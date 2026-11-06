// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Jose;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class BffSessionPrincipalTests : BaseTests
{
    private const string Issuer = "https://op.example";
    private const string Guard = "guard-1";

    private static BffSessionState CreateState() =>
        new(
            new string('a', 43),
            "store-key",
            1,
            new BffSessionMetadata { Issuer = Issuer, Subject = "sub-1" },
            new BffSessionPayload
            {
                LogoutGuard = Guard,
                Claims =
                [
                    new BffStoredClaim { Type = JoseClaimNames.Payload.Sub, Value = "sub-1" },
                ],
            }
        );

    #region SelectSessionClaims Tests

    [Theory]
    [InlineData(BffClaimTypes.LogoutUrl)]
    [InlineData(BffClaimTypes.LogoutSid)]
    public void SelectSessionClaims_BffClaimTypes_AreDropped(string claimType)
    {
        var identity = new ClaimsIdentity([
            new Claim(claimType, "value"),
            new Claim(JoseClaimNames.Payload.Sub, "sub-1"),
        ]);

        var result = BffSessionPrincipal.SelectSessionClaims(identity).ToList();

        var claim = Assert.Single(result);
        Assert.Equal(JoseClaimNames.Payload.Sub, claim.Type);
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_WithLogoutUrl_AddsLogoutClaims()
    {
        var state = CreateState();

        var principal = BffSessionPrincipal.Create(state, "/app/bff/logout");

        Assert.Equal("sub-1", principal.FindFirst(JoseClaimNames.Payload.Sub)?.Value);
        Assert.Equal("/app/bff/logout", principal.FindFirst(BffClaimTypes.LogoutUrl)?.Value);
        Assert.Equal(Guard, principal.FindFirst(BffClaimTypes.LogoutSid)?.Value);
        Assert.Equal(BffDefaults.AuthenticationScheme, principal.Identity?.AuthenticationType);
    }

    #endregion
}
