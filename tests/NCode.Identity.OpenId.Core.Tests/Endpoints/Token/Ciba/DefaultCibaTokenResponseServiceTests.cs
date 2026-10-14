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

using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.Ciba;

public class DefaultCibaTokenResponseServiceTests : BaseTests
{
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.Parse(
        "2026-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<ITokenService> MockTokenService { get; }
    private DefaultCibaTokenResponseService Service { get; }

    public DefaultCibaTokenResponseServiceTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockTokenService = CreateStrictMock<ITokenService>();
        Service = new DefaultCibaTokenResponseService(
            MockTimeProvider.Object,
            MockTokenService.Object
        );
    }

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private static SecurityToken CreateSecurityToken(string tokenValue) =>
        new()
        {
            TokenType = OpenIdConstants.TokenTypes.Bearer,
            TokenValue = tokenValue,
            TokenLifetime = new TimePeriod { StartTime = UtcNow, EndTime = UtcNow.AddHours(1) },
        };

    private static CibaGrant CreateGrant(params string[] scopes) =>
        new()
        {
            ClientId = "client-1",
            Scopes = scopes,
            Status = DeviceAuthorizationStatus.Approved,
            DeliveryMode = OpenIdConstants.BackchannelTokenDeliveryModes.Poll,
            SubjectAuthentication = CreateSubjectAuthentication(),
        };

    private (Mock<OpenIdContext> context, Mock<OpenIdClient> client) CreateScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();

        mockContext
            .SetupGet(x => x.Environment)
            .Returns(CreateStrictMock<OpenIdEnvironment>().Object)
            .Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        return (mockContext, mockClient);
    }

    private void SetupAccessToken(Mock<OpenIdContext> context, Mock<OpenIdClient> client) =>
        MockTokenService
            .Setup(x =>
                x.CreateAccessTokenAsync(
                    context.Object,
                    client.Object,
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken>(CreateSecurityToken("access-token-value")))
            .Verifiable();

    [Fact]
    public async Task CreateTokenResponseAsync_WhenApiScope_ReturnsAccessTokenOnly()
    {
        var (context, client) = CreateScaffold();
        SetupAccessToken(context, client);

        var result = await Service.CreateTokenResponseAsync(
            context.Object,
            client.Object,
            CreateGrant("api"),
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Equal("access-token-value", result.AccessToken);
        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, result.TokenType);
        Assert.Equal(["api"], result.Scopes);
        Assert.Null(result.IdToken);
        Assert.Null(result.RefreshToken);
    }

    [Fact]
    public async Task CreateTokenResponseAsync_WhenOpenIdAndOfflineAccess_ReturnsIdAndRefreshTokens()
    {
        var (context, client) = CreateScaffold();
        SetupAccessToken(context, client);

        MockTokenService
            .Setup(x =>
                x.CreateIdTokenAsync(
                    context.Object,
                    client.Object,
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken>(CreateSecurityToken("id-token-value")))
            .Verifiable();

        MockTokenService
            .Setup(x =>
                x.CreateRefreshTokenAsync(
                    context.Object,
                    client.Object,
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken>(CreateSecurityToken("refresh-token-value")))
            .Verifiable();

        var result = await Service.CreateTokenResponseAsync(
            context.Object,
            client.Object,
            CreateGrant(
                OpenIdConstants.ScopeTypes.OpenId,
                OpenIdConstants.ScopeTypes.OfflineAccess
            ),
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Equal("access-token-value", result.AccessToken);
        Assert.Equal("id-token-value", result.IdToken);
        Assert.Equal("refresh-token-value", result.RefreshToken);
        MockTokenService.Verify();
    }
}
