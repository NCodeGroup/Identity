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

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.RefreshToken;

public class DefaultValidateRefreshTokenGrantHandlerTests : BaseTests
{
    private const string ClientId = "client-1";
    private const string TenantId = "tenant-1";

    private Mock<IClientScopeService> MockClientScopeService { get; }
    private DefaultValidateRefreshTokenGrantHandler Handler { get; }

    public DefaultValidateRefreshTokenGrantHandlerTests()
    {
        MockClientScopeService = CreateStrictMock<IClientScopeService>();
        Handler = new DefaultValidateRefreshTokenGrantHandler(MockClientScopeService.Object);
    }

    #region Scaffolding

    // The allowed-scopes resolver is consulted only on paths past the extra-scopes check; the client-id
    // and extra-scopes checks throw before it is reached.
    private (
        ValidateTokenGrantCommand<RefreshTokenGrant> command,
        Mock<ITokenRequest> tokenRequest
    ) CreateScaffold(
        string grantClientId,
        IReadOnlyList<string> originalScopes,
        IReadOnlyCollection<string> allowedScopes
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();

        MockClientScopeService
            .Setup(x => x.GetAllowedScopesAsync(TenantId, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowedScopes);

        var command = new ValidateTokenGrantCommand<RefreshTokenGrant>(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            new RefreshTokenGrant(grantClientId, originalScopes, originalScopes, null)
        );

        return (command, mockTokenRequest);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenGrantValid_CompletesSuccessfully()
    {
        var (command, mockTokenRequest) = CreateScaffold(ClientId, ["api"], ["api"]);
        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenClientIdMismatch_ThrowsOpenIdException()
    {
        var (command, _) = CreateScaffold("different-client", ["api"], []);

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRequestedScopesExceedGranted_ThrowsOpenIdException()
    {
        var (command, mockTokenRequest) = CreateScaffold(ClientId, ["api"], []);
        mockTokenRequest.SetupGet(x => x.Scopes).Returns(["api", "extra"]).Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenEffectiveScopeNotAllowed_ThrowsOpenIdException()
    {
        var (command, mockTokenRequest) = CreateScaffold(ClientId, ["api"], []);
        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    #endregion
}
