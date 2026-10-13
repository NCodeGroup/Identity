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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class DefaultCibaRequestServiceTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string AuthReqId = "auth-req-id";

    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<ICibaNotificationService> MockNotificationService { get; }
    private DefaultCibaRequestService Service { get; }

    public DefaultCibaRequestServiceTests()
    {
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        MockNotificationService = CreateStrictMock<ICibaNotificationService>();
        Service = new DefaultCibaRequestService(
            MockPersistedGrantService.Object,
            MockNotificationService.Object
        );
    }

    private static PersistedGrantId CreateGrantId() =>
        new()
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.Ciba,
            GrantKey = AuthReqId,
        };

    private static SubjectAuthentication CreateSubject() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private static PersistedGrant<CibaGrant> CreateGrant(string status, string deliveryMode) =>
        new()
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = null,
            SessionId = null,
            Payload = new CibaGrant
            {
                ClientId = "client-1",
                Scopes = ["api"],
                Status = status,
                DeliveryMode = deliveryMode,
            },
        };

    private (Mock<OpenIdContext> context, Mock<OpenIdClient> client) CreateScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.Ciba, AuthReqId)
            )
            .Returns(CreateGrantId())
            .Verifiable();

        return (mockContext, mockClient);
    }

    private void SetupLookup(PersistedGrant<CibaGrant>? grant)
    {
        MockPersistedGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<CibaGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PersistedGrant<CibaGrant>?>(grant))
            .Verifiable();
    }

    [Fact]
    public async Task ApproveAsync_WhenPendingPoll_UpdatesToApprovedAndReturnsTrue()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Poll
            )
        );

        CibaGrant? captured = null;
        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CibaGrant>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (OpenIdContext _, PersistedGrantId _, CibaGrant payload, CancellationToken _) =>
                    captured = payload
            )
            .Returns(new ValueTask<bool>(true))
            .Verifiable();

        var result = await Service.ApproveAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateSubject(),
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(DeviceAuthorizationStatus.Approved, captured?.Status);
        Assert.Equal("subject-id", captured?.SubjectAuthentication?.SubjectId);
    }

    [Fact]
    public async Task ApproveAsync_WhenPendingPing_NotifiesClient()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            )
        );

        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CibaGrant>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<bool>(true))
            .Verifiable();

        MockNotificationService
            .Setup(x =>
                x.NotifyClientAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    AuthReqId,
                    It.IsAny<CibaGrant>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.ApproveAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateSubject(),
            CancellationToken.None
        );

        Assert.True(result);
        MockNotificationService.Verify();
    }

    [Fact]
    public async Task ApproveAsync_WhenNotFound_ReturnsFalse()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupLookup(null);

        var result = await Service.ApproveAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateSubject(),
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task DenyAsync_WhenPending_UpdatesToDeniedAndReturnsTrue()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Poll
            )
        );

        CibaGrant? captured = null;
        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CibaGrant>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (OpenIdContext _, PersistedGrantId _, CibaGrant payload, CancellationToken _) =>
                    captured = payload
            )
            .Returns(new ValueTask<bool>(true))
            .Verifiable();

        var result = await Service.DenyAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(DeviceAuthorizationStatus.Denied, captured?.Status);
    }
}
