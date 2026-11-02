// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class DefaultUnsupportedBackchannelAuthenticationHandlerTests : BaseTests
{
    [Fact]
    public async Task HandleAsync_Always_ReturnsUnknownUserIdError()
    {
        var handler = new DefaultUnsupportedBackchannelAuthenticationHandler();

        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockRequest = CreateStrictMock<IBackchannelAuthenticationRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        var capturedCode = (string?)null;
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => capturedCode = code)
            .Returns(mockError.Object);

        var command = new AuthenticateBackchannelRequestCommand(
            mockContext.Object,
            mockClient.Object,
            mockRequest.Object
        );

        var disposition = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
        Assert.Same(mockError.Object, disposition.Error);
        Assert.Equal(OpenIdProtocol.ErrorCodes.UnknownUserId, capturedCode);
    }
}
