// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class DefaultCibaNotificationServiceTests : BaseTests
{
    private DefaultCibaNotificationService Service { get; } = new();

    private static CibaGrant CreateGrant() =>
        new()
        {
            ClientId = "client-1",
            Scopes = ["openid", "api"],
            Status = DeviceAuthorizationStatus.Pending,
            DeliveryMode = OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
            ClientNotificationToken = "notify-token",
        };

    #region NotifyAuthenticationDeviceAsync Tests

    [Fact]
    public async Task NotifyAuthenticationDeviceAsync_Always_IsNoOp()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();

        await Service.NotifyAuthenticationDeviceAsync(
            mockContext.Object,
            mockClient.Object,
            "auth-req-id",
            CreateGrant(),
            CancellationToken.None
        );
    }

    #endregion
}
