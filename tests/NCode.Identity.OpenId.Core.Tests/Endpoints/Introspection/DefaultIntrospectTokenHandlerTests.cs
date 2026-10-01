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
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Authentication.Tenants;
using NCode.Identity.Secrets.Logic;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Introspection;

public class DefaultIntrospectTokenHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string Issuer = "https://issuer.example.com";
    private const string Token = "token-value";

    private Mock<IJsonWebTokenService> MockJsonWebTokenService { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private DefaultIntrospectTokenHandler Handler { get; }

    public DefaultIntrospectTokenHandlerTests()
    {
        MockJsonWebTokenService = CreateStrictMock<IJsonWebTokenService>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        Handler = new DefaultIntrospectTokenHandler(
            MockJsonWebTokenService.Object,
            MockPersistedGrantService.Object
        );
    }

    #region Scaffolding

    private IntrospectTokenCommand CreateCommand(string? token, IntrospectionResult result)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockRequest = CreateStrictMock<ITokenIntrospectionRequest>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        mockRequest.SetupGet(x => x.Token).Returns(token);

        return new IntrospectTokenCommand(
            mockContext.Object,
            mockClient.Object,
            mockRequest.Object,
            result
        );
    }

    private void SetupTenantForJwtValidation(OpenIdContext context)
    {
        var tenant = Mock.Get(context.Tenant);
        var mockSecretsProvider = CreateLooseMock<ISecretKeyCollectionProvider>();
        var mockSecretKeys = CreateLooseMock<ISecretKeyCollection>();

        tenant.SetupGet(x => x.Issuer).Returns(Issuer);
        tenant.SetupGet(x => x.SecretsProvider).Returns(mockSecretsProvider.Object);
        mockSecretsProvider.SetupGet(x => x.Collection).Returns(mockSecretKeys.Object);
    }

    private void SetupInvalidJwt() =>
        MockJsonWebTokenService
            .Setup(x =>
                x.ValidateJwtAsync(
                    Token,
                    It.IsAny<ValidateJwtParameters>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                ValidateJwtResult.Fail(
                    new ValidateJwtParameters(),
                    PropertyBagFactory.Create(),
                    Token,
                    new InvalidOperationException("invalid")
                )
            )
            .Verifiable();

    private void SetupCreateGrantId() =>
        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.RefreshToken, Token)
            )
            .Returns(
                new PersistedGrantId
                {
                    TenantId = TenantId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.RefreshToken,
                    GrantKey = Token,
                }
            )
            .Verifiable();

    private void SetupGetGrant(PersistedGrant<RefreshTokenGrant>? grant) =>
        MockPersistedGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<RefreshTokenGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(grant)
            .Verifiable();

    private static PersistedGrant<RefreshTokenGrant> CreateGrant(
        PersistedGrantStatus status,
        string? subjectId,
        IReadOnlyList<string> effectiveScopes
    ) =>
        new()
        {
            Status = status,
            TenantId = TenantId,
            ClientId = ClientId,
            SubjectId = subjectId,
            Payload = new RefreshTokenGrant(ClientId, [], effectiveScopes, null),
        };

    #endregion

    [Fact]
    public async Task HandleAsync_WhenAlreadyActive_ReturnsWithoutChecking()
    {
        var result = new IntrospectionResult { Active = true };
        var command = CreateCommand(Token, result);

        // Strict mocks ensure no validation or grant lookup occurs.
        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.Active);
    }

    [Fact]
    public async Task HandleAsync_WhenTokenMissing_LeavesInactive()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(token: null, result);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Active);
    }

    [Fact]
    public async Task HandleAsync_WhenNotJwtAndUnknownGrant_LeavesInactive()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result);
        SetupTenantForJwtValidation(command.OpenIdContext);
        SetupInvalidJwt();
        SetupCreateGrantId();
        SetupGetGrant(grant: null);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Active);
    }

    [Fact]
    public async Task HandleAsync_WhenNotJwtAndRevokedGrant_LeavesInactive()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result);
        SetupTenantForJwtValidation(command.OpenIdContext);
        SetupInvalidJwt();
        SetupCreateGrantId();
        SetupGetGrant(CreateGrant(PersistedGrantStatus.Revoked, subjectId: null, []));

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.Active);
    }

    [Fact]
    public async Task HandleAsync_WhenActiveRefreshToken_SetsActiveAndClaims()
    {
        var result = new IntrospectionResult { Active = false };
        var command = CreateCommand(Token, result);
        SetupTenantForJwtValidation(command.OpenIdContext);
        SetupInvalidJwt();
        SetupCreateGrantId();
        SetupGetGrant(
            CreateGrant(PersistedGrantStatus.Active, subjectId: "subject-1", ["api", "profile"])
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.Active);
        Assert.Equal(ClientId, result.Claims["client_id"]);
        Assert.Equal("api profile", result.Claims["scope"]);
        Assert.Equal("subject-1", result.Claims["sub"]);
    }
}
