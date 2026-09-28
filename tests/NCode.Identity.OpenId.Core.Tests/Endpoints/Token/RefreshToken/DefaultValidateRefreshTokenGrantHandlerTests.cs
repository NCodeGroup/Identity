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
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.RefreshToken;

public class DefaultValidateRefreshTokenGrantHandlerTests : BaseTests
{
    private const string ClientId = "client-1";

    private DefaultValidateRefreshTokenGrantHandler Handler { get; } = new();

    #region Scaffolding

    private ValidateTokenGrantCommand<RefreshTokenGrant> CreateCommand(
        string grantClientId,
        IReadOnlyList<string> originalScopes,
        List<string>? requestedScopes,
        IReadOnlyCollection<string> scopesSupported
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.ScopesSupported))
            .Returns(scopesSupported);

        mockTokenRequest.SetupGet(x => x.Scopes).Returns(requestedScopes);

        return new ValidateTokenGrantCommand<RefreshTokenGrant>(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            new RefreshTokenGrant(grantClientId, originalScopes, originalScopes, null)
        );
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenGrantValid_CompletesSuccessfully()
    {
        var command = CreateCommand(
            ClientId,
            ["api"],
            null,
            [OpenIdConstants.ScopeTypes.OfflineAccess, "api"]
        );

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenClientIdMismatch_ThrowsOpenIdException()
    {
        var command = CreateCommand(
            "different-client",
            ["api"],
            null,
            [OpenIdConstants.ScopeTypes.OfflineAccess, "api"]
        );

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenOfflineAccessNotSupported_ThrowsOpenIdException()
    {
        var command = CreateCommand(ClientId, ["api"], null, ["api"]);

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRequestedScopesExceedGranted_ThrowsOpenIdException()
    {
        var command = CreateCommand(
            ClientId,
            ["api"],
            ["api", "extra"],
            [OpenIdConstants.ScopeTypes.OfflineAccess, "api", "extra"]
        );

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenEffectiveScopeNotSupported_ThrowsOpenIdException()
    {
        var command = CreateCommand(
            ClientId,
            ["api"],
            null,
            [OpenIdConstants.ScopeTypes.OfflineAccess]
        );

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    #endregion
}
