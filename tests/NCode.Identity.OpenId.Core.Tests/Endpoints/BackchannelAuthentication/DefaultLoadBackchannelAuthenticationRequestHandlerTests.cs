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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Collections;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class DefaultLoadBackchannelAuthenticationRequestHandlerTests : BaseTests
{
    private Mock<IJwtValidator> MockJsonWebTokenService { get; }
    private DefaultLoadBackchannelAuthenticationRequestHandler Handler { get; }

    public DefaultLoadBackchannelAuthenticationRequestHandlerTests()
    {
        MockJsonWebTokenService = CreateStrictMock<IJwtValidator>();
        Handler = new DefaultLoadBackchannelAuthenticationRequestHandler(
            NullLogger<DefaultLoadBackchannelAuthenticationRequestHandler>.Instance,
            MockJsonWebTokenService.Object
        );
    }

    [Fact]
    public async Task HandleAsync_WhenNoRequestObject_ReturnsSameRequest()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockRequest = CreateStrictMock<IBackchannelAuthenticationRequest>();
        mockRequest.SetupGet(x => x.RequestJwt).Returns((string?)null).Verifiable();

        var result = await Handler.HandleAsync(
            new LoadBackchannelAuthenticationRequestCommand(
                mockContext.Object,
                mockClient.Object,
                mockRequest.Object
            ),
            CancellationToken.None
        );

        Assert.Same(mockRequest.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenRequestObjectInvalid_ThrowsOpenIdException()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSecretKeys = CreateLooseMock<ISecretKeyCollection>();
        var mockRequest = CreateStrictMock<IBackchannelAuthenticationRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockRequest.SetupGet(x => x.RequestJwt).Returns("a.b.c").Verifiable();
        mockContext.SetupGet(x => x.Environment).Returns(mockEnvironment.Object).Verifiable();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.Issuer).Returns("https://op.test").Verifiable();
        mockEnvironment.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockClient.SetupGet(x => x.SecretKeys).Returns(mockSecretKeys.Object).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns("client-1").Verifiable();

        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        MockJsonWebTokenService
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<string>(),
                    It.IsAny<JwtValidationParameters>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                JwtValidationResult.Fail(
                    new JwtValidationParameters(),
                    PropertyBagFactory.Create(),
                    "a.b.c",
                    new InvalidOperationException("tampered")
                )
            )
            .Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(() =>
            Handler
                .HandleAsync(
                    new LoadBackchannelAuthenticationRequestCommand(
                        mockContext.Object,
                        mockClient.Object,
                        mockRequest.Object
                    ),
                    CancellationToken.None
                )
                .AsTask()
        );
    }
}
