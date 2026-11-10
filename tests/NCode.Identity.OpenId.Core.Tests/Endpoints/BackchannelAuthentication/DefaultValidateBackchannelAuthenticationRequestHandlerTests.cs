// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

    // What the handler reads, in order; it stops at the first check that refuses the request, and a poll-mode request
    // ends at its delivery mode.
    private enum Reads
    {
        GrantTypes,
        Hints,
        DeliveryMode,
        NotificationToken,
        NotificationEndpoint,
    }

    private (
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client,
        Mock<IReadOnlySettingCollection> settings,
        Mock<IBackchannelAuthenticationRequest> request
    ) CreateScaffold(
        IReadOnlyCollection<string>? grantTypesSupported = null,
        string deliveryMode = OpenIdProtocol.BackchannelTokenDeliveryModes.Poll,
        IReadOnlyCollection<string>? deliveryModesSupported = null,
        string? loginHint = "user@localhost",
        string? loginHintToken = null,
        string? idTokenHint = null,
        string? clientNotificationToken = null,
        string? clientNotificationEndpoint = null,
        Reads? readsThrough = null
    )
    {
        var reads =
            readsThrough
            ?? (
                deliveryMode == OpenIdProtocol.BackchannelTokenDeliveryModes.Poll
                    ? Reads.DeliveryMode
                    : Reads.NotificationEndpoint
            );
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

        var grantTypes = grantTypesSupported ?? [OpenIdProtocol.GrantTypes.Ciba];
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.GrantTypesSupported, out grantTypes!))
            .Returns(true)
            .Verifiable();

        if (reads >= Reads.Hints)
        {
            mockRequest.SetupGet(x => x.LoginHint).Returns(loginHint).Verifiable();
            mockRequest.SetupGet(x => x.LoginHintToken).Returns(loginHintToken).Verifiable();
            mockRequest.SetupGet(x => x.IdTokenHint).Returns(idTokenHint).Verifiable();
        }

        if (reads >= Reads.DeliveryMode)
        {
            mockSettings
                .Setup(x => x.GetValue(OpenIdSettingKeys.BackchannelTokenDeliveryMode))
                .Returns(deliveryMode)
                .Verifiable();
            mockSettings
                .Setup(x => x.GetValue(OpenIdSettingKeys.BackchannelTokenDeliveryModesSupported))
                .Returns(
                    deliveryModesSupported
                        ??
                        [
                            OpenIdProtocol.BackchannelTokenDeliveryModes.Poll,
                            OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
                        ]
                )
                .Verifiable();
        }

        if (reads >= Reads.NotificationToken)
        {
            mockRequest
                .SetupGet(x => x.ClientNotificationToken)
                .Returns(clientNotificationToken)
                .Verifiable();
        }

        if (reads >= Reads.NotificationEndpoint)
        {
            mockSettings
                .Setup(x => x.GetValue(OpenIdSettingKeys.ClientNotificationEndpoint))
                .Returns(clientNotificationEndpoint ?? string.Empty)
                .Verifiable();
        }

        return (mockContext, mockClient, mockSettings, mockRequest);
    }

    private ValueTask InvokeAsync(
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client,
        Mock<IBackchannelAuthenticationRequest> request,
        CancellationToken cancellationToken
    ) =>
        Handler.HandleAsync(
            new ValidateBackchannelAuthenticationRequestCommand(
                context.Object,
                client.Object,
                request.Object
            ),
            cancellationToken
        );

    [Fact]
    public async Task HandleAsync_WhenValidPoll_DoesNotThrow()
    {
        var (context, client, _, request) = CreateScaffold();

        await InvokeAsync(context, client, request, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantNotAllowed_ThrowsUnauthorizedClient()
    {
        var (context, client, _, request) = CreateScaffold(
            grantTypesSupported: [OpenIdProtocol.GrantTypes.AuthorizationCode],
            readsThrough: Reads.GrantTypes
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request, CancellationToken.None).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.UnauthorizedClient, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenNoHint_ThrowsInvalidRequest()
    {
        var (context, client, _, request) = CreateScaffold(
            loginHint: null,
            readsThrough: Reads.Hints
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request, CancellationToken.None).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenMultipleHints_ThrowsInvalidRequest()
    {
        var (context, client, _, request) = CreateScaffold(
            loginHint: "user@localhost",
            idTokenHint: "an-id-token",
            readsThrough: Reads.Hints
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request, CancellationToken.None).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenUnsupportedDeliveryMode_ThrowsInvalidRequest()
    {
        var (context, client, _, request) = CreateScaffold(
            deliveryMode: OpenIdProtocol.BackchannelTokenDeliveryModes.Push,
            readsThrough: Reads.DeliveryMode
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request, CancellationToken.None).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenPingWithoutNotificationToken_ThrowsInvalidRequest()
    {
        var (context, client, _, request) = CreateScaffold(
            deliveryMode: OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
            clientNotificationToken: null,
            readsThrough: Reads.NotificationToken
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request, CancellationToken.None).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenPingWithoutNotificationEndpoint_ThrowsInvalidRequest()
    {
        var (context, client, _, request) = CreateScaffold(
            deliveryMode: OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
            clientNotificationToken: "notify-token",
            clientNotificationEndpoint: null
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            InvokeAsync(context, client, request, CancellationToken.None).AsTask()
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenValidPing_DoesNotThrow()
    {
        var (context, client, _, request) = CreateScaffold(
            deliveryMode: OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
            clientNotificationToken: "notify-token",
            clientNotificationEndpoint: "https://client.test/ciba"
        );

        await InvokeAsync(context, client, request, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenValidPush_DoesNotThrow()
    {
        var (context, client, _, request) = CreateScaffold(
            deliveryMode: OpenIdProtocol.BackchannelTokenDeliveryModes.Push,
            deliveryModesSupported:
            [
                OpenIdProtocol.BackchannelTokenDeliveryModes.Poll,
                OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
                OpenIdProtocol.BackchannelTokenDeliveryModes.Push,
            ],
            clientNotificationToken: "notify-token",
            clientNotificationEndpoint: "https://client.test/ciba"
        );

        await InvokeAsync(context, client, request, CancellationToken.None);
    }
}
