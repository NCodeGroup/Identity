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
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.DeviceAuthorization;

public class DefaultValidateDeviceAuthorizationRequestHandlerTests : BaseTests
{
    private DefaultValidateDeviceAuthorizationRequestHandler Handler { get; } = new();

    private string? CapturedErrorCode { get; set; }

    private (
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client,
        Mock<IDeviceAuthorizationRequest> request
    ) CreateScaffold(
        IReadOnlyCollection<string>? grantTypesSupported = null,
        bool grantTypesSupportedPresent = true
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        var mockRequest = CreateStrictMock<IDeviceAuthorizationRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();

        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => CapturedErrorCode = code)
            .Returns(mockError.Object);

        var grantTypes = grantTypesSupported ?? [OpenIdProtocol.GrantTypes.DeviceCode];
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.GrantTypesSupported, out grantTypes!))
            .Returns(grantTypesSupportedPresent);

        return (mockContext, mockClient, mockRequest);
    }

    private ValueTask InvokeAsync(
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client,
        Mock<IDeviceAuthorizationRequest> request
    ) =>
        Handler.HandleAsync(
            new ValidateDeviceAuthorizationRequestCommand(
                context.Object,
                client.Object,
                request.Object
            ),
            CancellationToken.None
        );

    [Fact]
    public async Task HandleAsync_WhenGrantAllowed_DoesNotThrow()
    {
        var (context, client, request) = CreateScaffold();

        await InvokeAsync(context, client, request);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantTypesNotConfigured_DoesNotThrow()
    {
        var (context, client, request) = CreateScaffold(grantTypesSupportedPresent: false);

        await InvokeAsync(context, client, request);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantNotAllowed_ThrowsUnauthorizedClient()
    {
        var (context, client, request) = CreateScaffold(
            grantTypesSupported: [OpenIdProtocol.GrantTypes.AuthorizationCode]
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.UnauthorizedClient, CapturedErrorCode);
    }

    [Fact]
    public void MediatorPriority_IsHigh()
    {
        Assert.Equal(DefaultMediatorPriorities.High, Handler.MediatorPriority);
    }
}
