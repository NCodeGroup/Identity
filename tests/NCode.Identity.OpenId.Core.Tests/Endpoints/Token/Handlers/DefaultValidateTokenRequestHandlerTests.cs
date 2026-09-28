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
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.Handlers;

public class DefaultValidateTokenRequestHandlerTests : BaseTests
{
    private DefaultValidateTokenRequestHandler Handler { get; } = new();

    #region Scaffolding

    private (
        ValidateTokenRequestCommand command,
        Mock<ITokenRequest> tokenRequest,
        Mock<IReadOnlySettingCollection> settings
    ) CreateScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        var command = new ValidateTokenRequestCommand(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object
        );

        return (command, mockTokenRequest, mockSettings);
    }

    private void SetupScopesSupported(
        Mock<IReadOnlySettingCollection> mockSettings,
        params string[] scopes
    )
    {
        IReadOnlyCollection<string> supported = scopes;
        mockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.ScopesSupported)).Returns(supported);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenScopesNull_CompletesSuccessfully()
    {
        var (command, mockTokenRequest, _) = CreateScaffold();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenScopesSupported_CompletesSuccessfully()
    {
        var (command, mockTokenRequest, mockSettings) = CreateScaffold();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns(["api"]).Verifiable();
        SetupScopesSupported(mockSettings, "api");

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenScopeNotSupported_ThrowsOpenIdException()
    {
        var (command, mockTokenRequest, mockSettings) = CreateScaffold();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns(["unsupported"]).Verifiable();
        SetupScopesSupported(mockSettings, "api");

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    #endregion
}
