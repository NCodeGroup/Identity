// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.SdJwt.Profiles;

namespace NCode.Identity.Jose.SdJwt.Tests.Profiles;

public class SdJwtProfileTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithARequiredClaimThatIsNotListedAsProtected_ProtectsIt()
    {
        var profile = new SdJwtProfile("test", "test+sd-jwt", ["sub"], ["status"]);

        Assert.Equal(["sub"], profile.RequiredClaims);
        Assert.Equal(["status", "sub"], profile.ProtectedClaims.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Constructor_WithAClaimBothRequiredAndProtected_ListsItOnce()
    {
        var profile = new SdJwtProfile("test", "test+sd-jwt", ["vct"], ["vct"]);

        Assert.Equal(["vct"], profile.ProtectedClaims);
    }

    #endregion
}
