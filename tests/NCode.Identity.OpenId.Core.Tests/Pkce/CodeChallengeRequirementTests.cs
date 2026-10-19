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
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Pkce;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Pkce;

public class CodeChallengeRequirementTests : BaseTests
{
    private const string Nonce = "n-0S6_WzA2Mj";

    private static readonly string[] OpenIdScopes = [OpenIdConstants.ScopeTypes.OpenId];

    #region Scaffolding

    private sealed record Policy(bool RequirePkce, bool NonceExemption);

    private static readonly Policy Relaxed = new(RequirePkce: false, NonceExemption: true);

    private Mock<OpenIdClient> CreateClient(Policy policy, string registeredAuthMethod = "")
    {
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RequireCodeChallenge))
            .Returns(policy.RequirePkce);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.PkceNonceExemption))
            .Returns(policy.NonceExemption);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns(registeredAuthMethod);

        var mockClient = CreateLooseMock<OpenIdClient>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);
        return mockClient;
    }

    private Mock<IAuthorizationRequest> CreateRequest(string[] scopes, string? nonce)
    {
        var mockRequest = CreateLooseMock<IAuthorizationRequest>();
        mockRequest.SetupGet(x => x.Scopes).Returns(scopes);
        mockRequest.SetupGet(x => x.Nonce).Returns(nonce);
        return mockRequest;
    }

    private bool IsRequiredForAuthorization(
        Policy policy,
        string registeredAuthMethod,
        string[] scopes,
        string? nonce
    )
    {
        var client = CreateClient(policy, registeredAuthMethod).Object;
        var request = CreateRequest(scopes, nonce).Object;
        return CodeChallengeRequirement.IsRequiredForAuthorization(client, request);
    }

    #endregion

    #region IsRequiredForAuthorization Tests

    [Fact]
    public void IsRequiredForAuthorization_WhenRequirePkceIsInForce_ReturnsTrue()
    {
        var policy = new Policy(RequirePkce: true, NonceExemption: true);

        var required = IsRequiredForAuthorization(
            policy,
            "client_secret_basic",
            OpenIdScopes,
            Nonce
        );

        Assert.True(required);
    }

    [Fact]
    public void IsRequiredForAuthorization_WhenRelaxedAttestedConfidentialAndUsesTheNonce_ReturnsFalse()
    {
        var required = IsRequiredForAuthorization(
            Relaxed,
            "client_secret_basic",
            OpenIdScopes,
            Nonce
        );

        Assert.False(required);
    }

    [Theory]
    [InlineData("")]
    [InlineData("none")]
    public void IsRequiredForAuthorization_WhenTheClientIsRegisteredAsPublic_ReturnsTrue(
        string registeredAuthMethod
    )
    {
        var required = IsRequiredForAuthorization(
            Relaxed,
            registeredAuthMethod,
            OpenIdScopes,
            Nonce
        );

        Assert.True(required);
    }

    [Fact]
    public void IsRequiredForAuthorization_WhenTheClientIsNotAttested_ReturnsTrue()
    {
        // Relaxing require_pkce permits the exemption; only an attested client receives it.
        var policy = new Policy(RequirePkce: false, NonceExemption: false);

        var required = IsRequiredForAuthorization(
            policy,
            "client_secret_basic",
            OpenIdScopes,
            Nonce
        );

        Assert.True(required);
    }

    [Fact]
    public void IsRequiredForAuthorization_WhenTheRequestHasNoNonce_ReturnsTrue()
    {
        var required = IsRequiredForAuthorization(
            Relaxed,
            "client_secret_basic",
            OpenIdScopes,
            nonce: null
        );

        Assert.True(required);
    }

    [Fact]
    public void IsRequiredForAuthorization_WhenTheRequestIsNotOpenIdConnect_ReturnsTrue()
    {
        var required = IsRequiredForAuthorization(Relaxed, "client_secret_basic", ["api"], Nonce);

        Assert.True(required);
    }

    #endregion

    #region IsRequiredForRedemption Tests

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void IsRequiredForRedemption_DependsOnTheAuthenticatedClientType(
        bool isConfidential,
        bool expected
    )
    {
        // The registered type says confidential; what counts at the token endpoint is how the client authenticated.
        var mockClient = CreateClient(Relaxed, "client_secret_basic");
        mockClient.SetupGet(x => x.IsConfidential).Returns(isConfidential);
        var request = CreateRequest(OpenIdScopes, Nonce).Object;

        var required = CodeChallengeRequirement.IsRequiredForRedemption(
            mockClient.Object,
            request,
            OpenIdScopes
        );

        Assert.Equal(expected, required);
    }

    [Fact]
    public void IsRequiredForRedemption_WhenTheEffectiveScopesDropOpenId_ReturnsTrue()
    {
        var mockClient = CreateClient(Relaxed, "client_secret_basic");
        mockClient.SetupGet(x => x.IsConfidential).Returns(true);
        var request = CreateRequest([OpenIdConstants.ScopeTypes.OpenId, "api"], Nonce).Object;

        var required = CodeChallengeRequirement.IsRequiredForRedemption(
            mockClient.Object,
            request,
            ["api"]
        );

        Assert.True(required);
    }

    #endregion
}
