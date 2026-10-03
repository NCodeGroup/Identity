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
using NCode.Identity.OpenId.Authentication.Endpoints.Token.ClientCredentials;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.ClientCredentials;

public class DefaultValidateClientCredentialsGrantHandlerTests : BaseTests
{
    private DefaultValidateClientCredentialsGrantHandler Handler { get; } = new();

    #region Scaffolding

    private ValidateTokenGrantCommand<ClientCredentialsGrant> CreateCommand(List<string>? scopes)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        mockTokenRequest.SetupGet(x => x.Scopes).Returns(scopes).Verifiable();

        return new ValidateTokenGrantCommand<ClientCredentialsGrant>(
            mockContext.Object,
            null!,
            mockTokenRequest.Object,
            default
        );
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNoForbiddenScopes_CompletesSuccessfully()
    {
        var command = CreateCommand(["api", "profile"]);

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenScopesNull_CompletesSuccessfully()
    {
        var command = CreateCommand(null);

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenOpenIdScopeRequested_ThrowsOpenIdException()
    {
        var command = CreateCommand([OpenIdConstants.ScopeTypes.OpenId]);

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenOfflineAccessScopeRequested_ThrowsOpenIdException()
    {
        var command = CreateCommand([OpenIdConstants.ScopeTypes.OfflineAccess]);

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    #endregion
}
