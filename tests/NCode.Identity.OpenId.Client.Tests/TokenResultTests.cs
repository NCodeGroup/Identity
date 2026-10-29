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
