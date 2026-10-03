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
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.Handlers;

public class DefaultSelectTokenGrantHandlerHandlerTests : BaseTests
{
    private const string GrantType = OpenIdConstants.GrantTypes.ClientCredentials;

    #region Scaffolding

    private Mock<ITokenGrantHandler> CreateGrantHandler(params string[] grantTypes)
    {
        var mockHandler = CreateStrictMock<ITokenGrantHandler>();
        IReadOnlySet<string> set = new HashSet<string>(grantTypes, StringComparer.Ordinal);
        mockHandler.SetupGet(x => x.GrantTypes).Returns(set).Verifiable();
        return mockHandler;
    }

    private (
        SelectTokenGrantHandlerCommand command,
        Mock<ITokenRequest> tokenRequest,
        Mock<IReadOnlySettingCollection> settings,
        Mock<IOpenIdError> error
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

        var command = new SelectTokenGrantHandlerCommand(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object
        );

        return (command, mockTokenRequest, mockSettings, mockError);
    }

    private void SetupGrantTypesSupported(
        Mock<IReadOnlySettingCollection> mockSettings,
        params string[] grantTypes
    )
    {
        IReadOnlyCollection<string> supported = grantTypes;
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.GrantTypesSupported, out supported))
            .Returns(true);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenGrantTypeMissing_ThrowsOpenIdException()
    {
        var (command, mockTokenRequest, _, _) = CreateScaffold();
        var handler = new DefaultSelectTokenGrantHandlerHandler([]);

        mockTokenRequest.SetupGet(x => x.GrantType).Returns((string?)null).Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenGrantTypeNotSupported_ThrowsOpenIdException()
    {
        var (command, mockTokenRequest, mockSettings, _) = CreateScaffold();
        var handler = new DefaultSelectTokenGrantHandlerHandler([]);

        mockTokenRequest.SetupGet(x => x.GrantType).Returns(GrantType).Verifiable();
        SetupGrantTypesSupported(mockSettings);

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNoHandlerForGrantType_ThrowsOpenIdException()
    {
        var (command, mockTokenRequest, mockSettings, _) = CreateScaffold();
        var handler = new DefaultSelectTokenGrantHandlerHandler([]);

        mockTokenRequest.SetupGet(x => x.GrantType).Returns(GrantType).Verifiable();
        SetupGrantTypesSupported(mockSettings, GrantType);

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenMultipleHandlersForGrantType_ThrowsOpenIdException()
    {
        var (command, mockTokenRequest, mockSettings, _) = CreateScaffold();
        var handler = new DefaultSelectTokenGrantHandlerHandler([
            CreateGrantHandler(GrantType).Object,
            CreateGrantHandler(GrantType).Object,
        ]);

        mockTokenRequest.SetupGet(x => x.GrantType).Returns(GrantType).Verifiable();
        SetupGrantTypesSupported(mockSettings, GrantType);

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenSingleHandlerForGrantType_ReturnsHandler()
    {
        var (command, mockTokenRequest, mockSettings, _) = CreateScaffold();
        var mockGrantHandler = CreateGrantHandler(GrantType);
        var handler = new DefaultSelectTokenGrantHandlerHandler([mockGrantHandler.Object]);

        mockTokenRequest.SetupGet(x => x.GrantType).Returns(GrantType).Verifiable();
        SetupGrantTypesSupported(mockSettings, GrantType);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Same(mockGrantHandler.Object, result);
    }

    #endregion
}
