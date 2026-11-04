// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.SdJwt.Profiles;

namespace NCode.Identity.Jose.SdJwt.Tests.Profiles;

public class SdJwtProfilesTests
{
    #region SdJwtVc Property Tests

    [Fact]
    public void SdJwtVc_IsTypedDcSdJwtAndRequiresVct()
    {
        var profile = SdJwtProfiles.SdJwtVc;

        Assert.Equal("dc+sd-jwt", profile.TokenType);
        Assert.Equal(["vct"], profile.RequiredClaims);
        Assert.Equal(
            ["cnf", "exp", "iss", "nbf", "status", "vct", "vct#integrity"],
            profile.ProtectedClaims.Order(StringComparer.Ordinal)
        );
        Assert.DoesNotContain("sub", (IReadOnlySet<string>)profile.ProtectedClaims);
        Assert.DoesNotContain("iat", (IReadOnlySet<string>)profile.ProtectedClaims);
    }

    #endregion
}
