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
