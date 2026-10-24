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

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.RefreshToken;

public class RefreshTokenRotationRequirementTests : BaseTests
{
    #region Scaffolding

    private Mock<OpenIdClient> CreateClient(string policy)
    {
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RefreshTokenRotationPolicy))
            .Returns(policy)
            .Verifiable();

        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        return mockClient;
    }

    private Mock<OpenIdClient> CreateClient(string policy, bool isConfidential)
    {
        var mockClient = CreateClient(policy);
        mockClient.SetupGet(x => x.IsConfidential).Returns(isConfidential).Verifiable();
        return mockClient;
    }

    #endregion

    #region IsRequired Tests

    [Fact]
    public void IsRequired_WhenThePolicyIsAlways_ReturnsTrueForABoundToken()
    {
        var mockClient = CreateClient(OpenIdConstants.RefreshTokenRotationPolicy.Always);
        var confirmation = new TokenConfirmation { JwkThumbprint = "jkt-1" };

        var required = RefreshTokenRotationRequirement.IsRequired(mockClient.Object, confirmation);

        Assert.True(required);
    }

    [Fact]
    public void IsRequired_WhenAPublicClientPresentsAnUnboundToken_ReturnsTrue()
    {
        var mockClient = CreateClient(
            OpenIdConstants.RefreshTokenRotationPolicy.Unbound,
            isConfidential: false
        );

        var required = RefreshTokenRotationRequirement.IsRequired(
            mockClient.Object,
            TokenConfirmation.None
        );

        Assert.True(required);
    }

    [Fact]
    public void IsRequired_WhenAPublicClientPresentsADPoPBoundToken_ReturnsFalse()
    {
        var mockClient = CreateClient(
            OpenIdConstants.RefreshTokenRotationPolicy.Unbound,
            isConfidential: false
        );
        var confirmation = new TokenConfirmation { JwkThumbprint = "jkt-1" };

        var required = RefreshTokenRotationRequirement.IsRequired(mockClient.Object, confirmation);

        Assert.False(required);
    }

    [Fact]
    public void IsRequired_WhenAPublicClientPresentsACertificateBoundToken_ReturnsFalse()
    {
        var mockClient = CreateClient(
            OpenIdConstants.RefreshTokenRotationPolicy.Unbound,
            isConfidential: false
        );
        var confirmation = new TokenConfirmation { X509CertificateSha256Thumbprint = "x5t-1" };

        var required = RefreshTokenRotationRequirement.IsRequired(mockClient.Object, confirmation);

        Assert.False(required);
    }

    [Fact]
    public void IsRequired_WhenAConfidentialClientPresentsAnUnboundToken_ReturnsFalse()
    {
        var mockClient = CreateClient(
            OpenIdConstants.RefreshTokenRotationPolicy.Unbound,
            isConfidential: true
        );

        var required = RefreshTokenRotationRequirement.IsRequired(
            mockClient.Object,
            TokenConfirmation.None
        );

        Assert.False(required);
    }

    #endregion
}
