// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.OpenId.Client.Tests;

public class TokenResultTests
{
    #region DPoPJwkThumbprint Tests

    [Fact]
    public void DPoPJwkThumbprint_WhenNotSet_ThenNull()
    {
        var result = new TokenResult(new TokenResponse { AccessToken = "at", TokenType = "DPoP" });

        Assert.Null(result.DPoPJwkThumbprint);
    }

    [Fact]
    public void DPoPJwkThumbprint_WhenSet_ThenValue()
    {
        var result = new TokenResult(new TokenResponse { AccessToken = "at", TokenType = "DPoP" })
        {
            DPoPJwkThumbprint = "jkt-1",
        };

        Assert.Equal("jkt-1", result.DPoPJwkThumbprint);
    }

    #endregion
}
