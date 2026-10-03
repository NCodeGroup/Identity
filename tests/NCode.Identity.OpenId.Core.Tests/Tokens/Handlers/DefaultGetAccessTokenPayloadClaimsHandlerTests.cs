#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.Jose;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Handlers;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.Handlers;

public class DefaultGetAccessTokenPayloadClaimsHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private static readonly DateTimeOffset CreatedWhen = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<ICryptoService> MockCryptoService { get; }
    private DefaultGetAccessTokenPayloadClaimsHandler Handler { get; }

    public DefaultGetAccessTokenPayloadClaimsHandlerTests()
    {
        MockCryptoService = CreateStrictMock<ICryptoService>();
        Handler = new DefaultGetAccessTokenPayloadClaimsHandler(MockCryptoService.Object);
    }

    #region Scaffolding

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private (
        GetAccessTokenPayloadClaimsCommand command,
        Dictionary<string, object> payloadClaims
    ) CreateCommand(SubjectAuthentication? subjectAuthentication, IReadOnlyList<string> scopes)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();

        var tokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = CreatedWhen,
            GrantType = OpenIdConstants.GrantTypes.AuthorizationCode,
            OriginalScopes = scopes,
            EffectiveScopes = scopes,
            SubjectAuthentication = subjectAuthentication,
        };

        var tokenContext = new SecurityTokenContext(
            tokenRequest,
            SigningCredentials: null!,
            EncryptionCredentials: null,
            TokenType: OpenIdConstants.TokenTypes.AccessToken
        );

        var payloadClaims = new Dictionary<string, object>(StringComparer.Ordinal);
        var command = new GetAccessTokenPayloadClaimsCommand(
            mockContext.Object,
            mockClient.Object,
            tokenContext,
            payloadClaims
        );

        return (command, payloadClaims);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenSubjectPresent_PopulatesProtocolClaims()
    {
        var (command, payloadClaims) = CreateCommand(
            CreateSubjectAuthentication(),
            ["openid", "api"]
        );

        MockCryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), It.IsAny<BinaryEncodingType>()))
            .Returns("token-id")
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("token-id", payloadClaims[JoseClaimNames.Payload.Jti]);
        Assert.Equal(TenantId, payloadClaims[JoseClaimNames.Payload.Tid]);
        Assert.Equal(ClientId, payloadClaims[JoseClaimNames.Payload.ClientId]);
        Assert.Equal("openid api", payloadClaims[JoseClaimNames.Payload.Scope]);
    }

    [Fact]
    public async Task HandleAsync_WhenNoSubject_OmitsOfflineAccessScope()
    {
        var (command, payloadClaims) = CreateCommand(
            subjectAuthentication: null,
            ["openid", OpenIdConstants.ScopeTypes.OfflineAccess]
        );

        MockCryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), It.IsAny<BinaryEncodingType>()))
            .Returns("token-id")
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("openid", payloadClaims[JoseClaimNames.Payload.Scope]);
    }

    [Fact]
    public async Task HandleAsync_WhenJtiAlreadyPresent_DoesNotRegenerate()
    {
        var (command, payloadClaims) = CreateCommand(CreateSubjectAuthentication(), ["openid"]);
        payloadClaims[JoseClaimNames.Payload.Jti] = "existing-id";

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("existing-id", payloadClaims[JoseClaimNames.Payload.Jti]);
    }

    #endregion
}
