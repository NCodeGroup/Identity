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
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class DefaultValidateBackchannelAuthenticationRequestHandlerTests : BaseTests
{
    private DefaultValidateBackchannelAuthenticationRequestHandler Handler { get; } = new();

    private string? CapturedErrorCode { get; set; }

    private (
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client,
        Mock<IReadOnlySettingCollection> settings,
        Mock<IBackchannelAuthenticationRequest> request
    ) CreateScaffold(
        IReadOnlyCollection<string>? grantTypesSupported = null,
        string deliveryMode = OpenIdConstants.BackchannelTokenDeliveryModes.Poll,
        IReadOnlyCollection<string>? deliveryModesSupported = null,
        string? loginHint = "user@localhost",
        string? loginHintToken = null,
        string? idTokenHint = null,
        string? clientNotificationToken = null,
        string? clientNotificationEndpoint = null
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        var mockRequest = CreateStrictMock<IBackchannelAuthenticationRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();

        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => CapturedErrorCode = code)
            .Returns(mockError.Object);

        var grantTypes = grantTypesSupported ?? [OpenIdConstants.GrantTypes.Ciba];
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.GrantTypesSupported, out grantTypes!))
            .Returns(true);

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.BackchannelTokenDeliveryMode))
            .Returns(deliveryMode);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.BackchannelTokenDeliveryModesSupported))
            .Returns(
                deliveryModesSupported
                    ??
                    [
                        OpenIdConstants.BackchannelTokenDeliveryModes.Poll,
                        OpenIdConstants.BackchannelTokenDeliveryModes.Ping,
                    ]
            );
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.ClientNotificationEndpoint))
            .Returns(clientNotificationEndpoint ?? string.Empty);

        mockRequest.SetupGet(x => x.LoginHint).Returns(loginHint);
        mockRequest.SetupGet(x => x.LoginHintToken).Returns(loginHintToken);
        mockRequest.SetupGet(x => x.IdTokenHint).Returns(idTokenHint);
        mockRequest.SetupGet(x => x.ClientNotificationToken).Returns(clientNotificationToken);

        return (mockContext, mockClient, mockSettings, mockRequest);
    }

    private ValueTask InvokeAsync(
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client,
        Mock<IBackchannelAuthenticationRequest> request
    ) =>
        Handler.HandleAsync(
            new ValidateBackchannelAuthenticationRequestCommand(
                context.Object,
                client.Object,
                request.Object
            ),
            CancellationToken.None
        );

    [Fact]
    public async Task HandleAsync_WhenValidPoll_DoesNotThrow()
    {
        var (context, client, _, request) = CreateScaffold();

        await InvokeAsync(context, client, request);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantNotAllowed_ThrowsUnauthorizedClient()
    {
        var (context, client, _, request) = CreateScaffold(
            grantTypesSupported: [OpenIdConstants.GrantTypes.AuthorizationCode]
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.UnauthorizedClient, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenNoHint_ThrowsInvalidRequest()
    {
        var (context, client, _, request) = CreateScaffold(loginHint: null);

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenMultipleHints_ThrowsInvalidRequest()
    {
        var (context, client, _, request) = CreateScaffold(
            loginHint: "user@localhost",
            idTokenHint: "an-id-token"
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenUnsupportedDeliveryMode_ThrowsInvalidRequest()
    {
        var (context, client, _, request) = CreateScaffold(
            deliveryMode: OpenIdConstants.BackchannelTokenDeliveryModes.Push
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenPingWithoutNotificationToken_ThrowsInvalidRequest()
    {
        var (context, client, _, request) = CreateScaffold(
            deliveryMode: OpenIdConstants.BackchannelTokenDeliveryModes.Ping,
            clientNotificationToken: null
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenPingWithoutNotificationEndpoint_ThrowsInvalidRequest()
    {
        var (context, client, _, request) = CreateScaffold(
            deliveryMode: OpenIdConstants.BackchannelTokenDeliveryModes.Ping,
            clientNotificationToken: "notify-token",
            clientNotificationEndpoint: null
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenValidPing_DoesNotThrow()
    {
        var (context, client, _, request) = CreateScaffold(
            deliveryMode: OpenIdConstants.BackchannelTokenDeliveryModes.Ping,
            clientNotificationToken: "notify-token",
            clientNotificationEndpoint: "https://client.test/ciba"
        );

        await InvokeAsync(context, client, request);
    }

    [Fact]
    public async Task HandleAsync_WhenValidPush_DoesNotThrow()
    {
        var (context, client, _, request) = CreateScaffold(
            deliveryMode: OpenIdConstants.BackchannelTokenDeliveryModes.Push,
            deliveryModesSupported:
            [
                OpenIdConstants.BackchannelTokenDeliveryModes.Poll,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping,
                OpenIdConstants.BackchannelTokenDeliveryModes.Push,
            ],
            clientNotificationToken: "notify-token",
            clientNotificationEndpoint: "https://client.test/ciba"
        );

        await InvokeAsync(context, client, request);
    }
}
