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
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Tenants;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.Handlers;

public class DefaultValidateTokenRequestHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private Mock<IClientScopeService> MockClientScopeService { get; }
    private DefaultValidateTokenRequestHandler Handler { get; }

    public DefaultValidateTokenRequestHandlerTests()
    {
        MockClientScopeService = CreateStrictMock<IClientScopeService>();
        Handler = new DefaultValidateTokenRequestHandler(MockClientScopeService.Object);
    }

    #region Scaffolding

    private (ValidateTokenRequestCommand command, Mock<ITokenRequest> tokenRequest) CreateScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId);
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        var command = new ValidateTokenRequestCommand(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object
        );

        return (command, mockTokenRequest);
    }

    private void SetupAllowedScopes(params string[] scopes)
    {
        MockClientScopeService
            .Setup(x => x.GetAllowedScopesAsync(TenantId, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scopes);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenScopesNull_CompletesSuccessfully()
    {
        var (command, mockTokenRequest) = CreateScaffold();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenScopesAllowed_CompletesSuccessfully()
    {
        var (command, mockTokenRequest) = CreateScaffold();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns(["api"]).Verifiable();
        SetupAllowedScopes("api");

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenScopeNotAllowed_ThrowsOpenIdException()
    {
        var (command, mockTokenRequest) = CreateScaffold();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns(["unsupported"]).Verifiable();
        SetupAllowedScopes("api");

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    #endregion
}
