#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.OpenId.Authentication.Tokens.Models;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.ProofOfPossession;

public class TokenConfirmationTests
{
    #region HasValue Property Tests

    [Fact]
    public void HasValue_WhenEmpty_ReturnsFalse()
    {
        var confirmation = TokenConfirmation.None;

        Assert.False(confirmation.HasValue);
    }

    [Fact]
    public void HasValue_WhenJwkThumbprintSet_ReturnsTrue()
    {
        var confirmation = new TokenConfirmation { JwkThumbprint = "jkt-value" };

        Assert.True(confirmation.HasValue);
    }

    [Fact]
    public void HasValue_WhenCertificateThumbprintSet_ReturnsTrue()
    {
        var confirmation = new TokenConfirmation { X509CertificateSha256Thumbprint = "x5t-value" };

        Assert.True(confirmation.HasValue);
    }

    #endregion

    #region ToClaimValueOrNull Tests

    [Fact]
    public void ToClaimValueOrNull_WhenEmpty_ReturnsNull()
    {
        var confirmation = TokenConfirmation.None;

        Assert.Null(confirmation.ToClaimValueOrNull());
    }

    [Fact]
    public void ToClaimValueOrNull_WhenJwkThumbprintSet_ReturnsJktMember()
    {
        var confirmation = new TokenConfirmation { JwkThumbprint = "jkt-value" };

        var claim = confirmation.ToClaimValueOrNull();

        Assert.NotNull(claim);
        Assert.Equal("jkt-value", claim[OpenIdProtocol.ConfirmationMethods.JwkThumbprint]);
        Assert.Single(claim);
    }

    [Fact]
    public void ToClaimValueOrNull_WhenBothSet_ReturnsBothMembers()
    {
        var confirmation = new TokenConfirmation
        {
            JwkThumbprint = "jkt-value",
            X509CertificateSha256Thumbprint = "x5t-value",
        };

        var claim = confirmation.ToClaimValueOrNull();

        Assert.NotNull(claim);
        Assert.Equal("jkt-value", claim[OpenIdProtocol.ConfirmationMethods.JwkThumbprint]);
        Assert.Equal(
            "x5t-value",
            claim[OpenIdProtocol.ConfirmationMethods.X509CertificateSha256Thumbprint]
        );
        Assert.Equal(2, claim.Count);
    }

    #endregion
}
