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
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Authentication.Tenants;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Revocation;

public class DefaultRevokeTokenHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string Token = "refresh-token-value";

    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private DefaultRevokeTokenHandler Handler { get; }

    public DefaultRevokeTokenHandlerTests()
    {
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        Handler = new DefaultRevokeTokenHandler(
            MockPersistedGrantService.Object,
            TimeProvider.System
        );
    }

    #region Scaffolding

    private (RevokeTokenCommand command, Mock<ITokenRevocationRequest> request) CreateScaffold(
        string? token
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockRequest = CreateStrictMock<ITokenRevocationRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId);
        mockRequest.SetupGet(x => x.Token).Returns(token);
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        var command = new RevokeTokenCommand(
            mockContext.Object,
            mockClient.Object,
            mockRequest.Object
        );

        return (command, mockRequest);
    }

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

    private static PersistedGrant<RefreshTokenGrant> CreateGrant(string clientId) =>
        new()
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = clientId,
            SubjectId = null,
            Payload = new RefreshTokenGrant(clientId, [], [], null),
        };

    #endregion

    [Fact]
    public async Task HandleAsync_WhenTokenMissing_ThrowsOpenIdException()
    {
        var (command, _) = CreateScaffold(token: null);

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenOwnRefreshToken_RevokesIt()
    {
        var (command, _) = CreateScaffold(Token);
        SetupCreateGrantId();
        SetupGetGrant(CreateGrant(ClientId));
        MockPersistedGrantService
            .Setup(x =>
                x.SetRevokedOnceAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTokenUnknown_DoesNotRevoke()
    {
        var (command, _) = CreateScaffold(Token);
        SetupCreateGrantId();
        SetupGetGrant(grant: null);

        // No SetRevokedOnceAsync setup: the strict mock fails if revocation is attempted.
        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTokenBelongsToAnotherClient_DoesNotRevoke()
    {
        var (command, _) = CreateScaffold(Token);
        SetupCreateGrantId();
        SetupGetGrant(CreateGrant("other-client"));

        await Handler.HandleAsync(command, CancellationToken.None);
    }
}
