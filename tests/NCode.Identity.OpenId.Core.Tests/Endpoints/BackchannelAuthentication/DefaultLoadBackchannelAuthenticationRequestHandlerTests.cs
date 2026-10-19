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
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Settings;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class DefaultLoadBackchannelAuthenticationRequestHandlerTests : BaseTests
{
    private Mock<IJwtValidator> MockJwtValidator { get; }
    private Mock<IClientKeyResolver> MockClientKeyResolver { get; }
    private DefaultLoadBackchannelAuthenticationRequestHandler Handler { get; }

    public DefaultLoadBackchannelAuthenticationRequestHandlerTests()
    {
        MockJwtValidator = CreateStrictMock<IJwtValidator>();
        MockClientKeyResolver = CreateStrictMock<IClientKeyResolver>();
        Handler = new DefaultLoadBackchannelAuthenticationRequestHandler(
            NullLogger<DefaultLoadBackchannelAuthenticationRequestHandler>.Instance,
            MockJwtValidator.Object,
            MockClientKeyResolver.Object
        );
    }

    #region Scaffolding

    private (
        LoadBackchannelAuthenticationRequestCommand Command,
        ISecretKeyCollection ClientKeys
    ) CreateSignedRequestScaffold(IReadOnlyCollection<string>? permittedAlgorithms)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        var clientKeys = CreateLooseMock<ISecretKeyCollection>().Object;
        var mockRequest = CreateStrictMock<IBackchannelAuthenticationRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockRequest.SetupGet(x => x.RequestJwt).Returns("a.b.c").Verifiable();
        mockContext.SetupGet(x => x.Environment).Returns(mockEnvironment.Object).Verifiable();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.Issuer).Returns("https://op.test").Verifiable();
        mockEnvironment.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns("client-1").Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        var permitted = permittedAlgorithms ?? [];
        mockSettings
            .Setup(x =>
                x.TryGetValue(
                    OpenIdSettingKeys.BackchannelAuthenticationRequestSigningAlgValuesSupported,
                    out permitted
                )
            )
            .Returns(permittedAlgorithms is not null)
            .Verifiable();

        MockClientKeyResolver
            .Setup(x => x.ResolveSecretKeysAsync(mockClient.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clientKeys)
            .Verifiable();

        var command = new LoadBackchannelAuthenticationRequestCommand(
            mockContext.Object,
            mockClient.Object,
            mockRequest.Object
        );

        return (command, clientKeys);
    }

    private JwtValidationParameters? CapturedParameters { get; set; }

    private void SetupValidatorFailure() =>
        MockJwtValidator
            .Setup(x =>
                x.ValidateAsync(
                    "a.b.c",
                    It.IsAny<JwtValidationParameters>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, JwtValidationParameters, CancellationToken>(
                (_, parameters, _) => CapturedParameters = parameters
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
    #endregion

    #region HandleAsync Tests

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
        var (command, _) = CreateSignedRequestScaffold(["PS256"]);
        SetupValidatorFailure();

        await Assert.ThrowsAsync<OpenIdException>(() =>
            Handler.HandleAsync(command, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRequestObject_VerifiesWithClientKeysAndPermittedAlgorithmsOnly()
    {
        IReadOnlyCollection<string> permitted = ["PS256", "ES256"];
        var (command, clientKeys) = CreateSignedRequestScaffold(permitted);
        SetupValidatorFailure();

        await Assert.ThrowsAsync<OpenIdException>(() =>
            Handler.HandleAsync(command, CancellationToken.None).AsTask()
        );

        Assert.NotNull(CapturedParameters);
        Assert.Same(clientKeys, CapturedParameters.ValidationKeys);
        Assert.Equal(permitted, CapturedParameters.ValidSignatureAlgorithms);
        // CIBA defines no encrypted authentication request, so no decryption keys are given.
        Assert.Null(CapturedParameters.DecryptionKeys);
    }

    [Fact]
    public async Task HandleAsync_WhenSigningAlgorithmsUnset_PermitsNone()
    {
        var (command, _) = CreateSignedRequestScaffold(permittedAlgorithms: null);
        SetupValidatorFailure();

        await Assert.ThrowsAsync<OpenIdException>(() =>
            Handler.HandleAsync(command, CancellationToken.None).AsTask()
        );

        Assert.NotNull(CapturedParameters);
        Assert.NotNull(CapturedParameters.ValidSignatureAlgorithms);
        Assert.Empty(CapturedParameters.ValidSignatureAlgorithms);
    }

    #endregion
}
