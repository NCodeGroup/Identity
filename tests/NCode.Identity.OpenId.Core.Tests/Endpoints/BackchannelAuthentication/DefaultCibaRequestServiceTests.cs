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
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class DefaultCibaRequestServiceTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string AuthReqId = "auth-req-id";
    private const string Endpoint = "https://client.test/ciba";

    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<IJobDispatcher> MockJobDispatcher { get; }
    private Mock<ICibaTokenResponseService> MockTokenResponseService { get; }
    private DefaultCibaRequestService Service { get; }

    public DefaultCibaRequestServiceTests()
    {
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        MockJobDispatcher = CreateStrictMock<IJobDispatcher>();
        MockTokenResponseService = CreateStrictMock<ICibaTokenResponseService>();
        Service = new DefaultCibaRequestService(
            MockPersistedGrantService.Object,
            MockJobDispatcher.Object,
            MockTokenResponseService.Object
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

    private void SetupSubjectValidation(Mock<OpenIdContext> mockContext, bool refused = false)
    {
        var mockMediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();

        var mockError = CreateLooseMock<IOpenIdError>();
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateSubjectAuthenticationCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ValidateSubjectAuthenticationCommand command, CancellationToken _) =>
                {
                    if (refused)
                        command.OperationDisposition.Error = mockError.Object;
                }
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    private Func<PersistedGrantUpdate<CibaGrant>?> SetupDecision(
        PersistedGrant<CibaGrant> stored,
        bool decided = true
    )
    {
        Func<PersistedGrant<CibaGrant>, PersistedGrantUpdate<CibaGrant>?>? captured = null;

        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync<CibaGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<Func<PersistedGrant<CibaGrant>, PersistedGrantUpdate<CibaGrant>?>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    Func<PersistedGrant<CibaGrant>, PersistedGrantUpdate<CibaGrant>?> update,
                    CancellationToken _
                ) => captured = update
            )
            .ReturnsAsync(decided)
            .Verifiable();

        return () => captured?.Invoke(stored);
    }

    private void SetupClientEndpoint(Mock<OpenIdClient> mockClient, string? endpoint)
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockClient.SetupGet(x => x.ClientId).Returns("client-1").Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.ClientNotificationEndpoint))
            .Returns(endpoint ?? string.Empty)
            .Verifiable();
    }

    private CibaDeliveryJob? CapturedJob { get; set; }

    private void SetupDispatch()
    {
        MockJobDispatcher
            .Setup(x =>
                x.DispatchAsync(
                    It.IsAny<CibaDeliveryJob>(),
                    It.IsAny<DispatchOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (CibaDeliveryJob job, DispatchOptions? _, CancellationToken _) => CapturedJob = job
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    [Fact]
    public async Task ApproveAsync_WhenPendingPoll_UpdatesToApprovedAndReturnsTrue()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupSubjectValidation(mockContext);
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Poll
            )
        );

        var update = SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Poll
            )
        );

        var result = await Service.ApproveAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateSubject(),
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(DeviceAuthorizationStatus.Approved, update()?.Payload.Status);
        Assert.Equal("subject-id", update()?.Payload.SubjectAuthentication?.PrincipalId);
    }

    [Fact]
    public async Task ApproveAsync_WhenAnotherRequestDecidedFirst_ReturnsFalseWithoutDelivering()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupSubjectValidation(mockContext);
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            )
        );

        SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Approved,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            ),
            decided: false
        );

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
    public async Task ApproveAsync_WhenTheStoredRequestIsNoLongerPending_MakesNoChange()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupSubjectValidation(mockContext);
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Poll
            )
        );

        var update = SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Approved,
                OpenIdConstants.BackchannelTokenDeliveryModes.Poll
            ),
            decided: false
        );

        var result = await Service.ApproveAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateSubject(),
            CancellationToken.None
        );

        Assert.False(result);
        Assert.Null(update());
    }

    [Fact]
    public async Task ApproveAsync_WhenPendingPing_NotifiesClient()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupSubjectValidation(mockContext);
        SetupClientEndpoint(mockClient, Endpoint);
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            )
        );

        SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            )
        );

        SetupDispatch();

        var result = await Service.ApproveAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateSubject(),
            CancellationToken.None
        );

        Assert.True(result);
        MockJobDispatcher.Verify();
        Assert.Equal(OpenIdConstants.BackchannelTokenDeliveryModes.Ping, CapturedJob?.DeliveryMode);
        Assert.Equal(Endpoint, CapturedJob?.EndpointUrl);
        Assert.Equal(AuthReqId, CapturedJob?.AuthReqId);
        Assert.Null(CapturedJob?.AccessToken);
    }

    [Fact]
    public async Task ApproveAsync_WhenPendingPush_IssuesAndDeliversTokens()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupSubjectValidation(mockContext);
        SetupClientEndpoint(mockClient, Endpoint);
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Push
            )
        );

        SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Push
            )
        );

        var mockTokenResponse = CreateStrictMock<ITokenResponse>();
        mockTokenResponse.SetupGet(x => x.AccessToken).Returns("access-token-value");
        mockTokenResponse.SetupGet(x => x.TokenType).Returns(OpenIdConstants.TokenTypes.Bearer);
        mockTokenResponse.SetupGet(x => x.ExpiresIn).Returns(TimeSpan.FromHours(1));
        mockTokenResponse.SetupGet(x => x.RefreshToken).Returns((string?)null);
        mockTokenResponse.SetupGet(x => x.IdToken).Returns("id-token-value");
        mockTokenResponse.SetupGet(x => x.Scopes).Returns(["openid", "api"]);

        MockTokenResponseService
            .Setup(x =>
                x.CreateTokenResponseAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    It.IsAny<CibaGrant>(),
                    It.IsAny<ProofOfPossessionBinding>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<ITokenResponse>(mockTokenResponse.Object))
            .Verifiable();

        SetupDispatch();

        var result = await Service.ApproveAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateSubject(),
            CancellationToken.None
        );

        Assert.True(result);
        MockTokenResponseService.Verify();
        MockJobDispatcher.Verify();
        Assert.Equal(OpenIdConstants.BackchannelTokenDeliveryModes.Push, CapturedJob?.DeliveryMode);
        Assert.Equal("access-token-value", CapturedJob?.AccessToken);
        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, CapturedJob?.TokenType);
        Assert.Equal(3600, CapturedJob?.ExpiresInSeconds);
        Assert.Null(CapturedJob?.RefreshToken);
        Assert.Equal("id-token-value", CapturedJob?.IdToken);
        Assert.Equal("openid api", CapturedJob?.Scope);
    }

    [Fact]
    public async Task ApproveAsync_WhenTheApprovingSubjectIsRefused_DeniesTheRequestAndReturnsFalse()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupSubjectValidation(mockContext, refused: true);
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Poll
            )
        );

        var update = SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Poll
            )
        );

        var result = await Service.ApproveAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateSubject(),
            CancellationToken.None
        );

        Assert.False(result);
        Assert.Equal(DeviceAuthorizationStatus.Denied, update()?.Payload.Status);
    }

    [Fact]
    public async Task ApproveAsync_WhenTheApprovingSubjectIsRefused_NotifiesAPingClientOfTheDenial()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupSubjectValidation(mockContext, refused: true);
        SetupClientEndpoint(mockClient, Endpoint);
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            )
        );

        SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            )
        );

        SetupDispatch();

        var result = await Service.ApproveAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CreateSubject(),
            CancellationToken.None
        );

        Assert.False(result);
        MockJobDispatcher.Verify();
        Assert.Equal(OpenIdConstants.BackchannelTokenDeliveryModes.Ping, CapturedJob?.DeliveryMode);
        Assert.Equal(AuthReqId, CapturedJob?.AuthReqId);
        Assert.Null(CapturedJob?.Error);
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

        var update = SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Poll
            )
        );

        var result = await Service.DenyAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(DeviceAuthorizationStatus.Denied, update()?.Payload.Status);
    }

    [Fact]
    public async Task DenyAsync_WhenPendingPing_NotifiesTheClient()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupClientEndpoint(mockClient, Endpoint);
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            )
        );

        SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            )
        );

        SetupDispatch();

        var result = await Service.DenyAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CancellationToken.None
        );

        Assert.True(result);
        MockJobDispatcher.Verify();
        Assert.Equal(OpenIdConstants.BackchannelTokenDeliveryModes.Ping, CapturedJob?.DeliveryMode);
        Assert.Equal(Endpoint, CapturedJob?.EndpointUrl);
        Assert.Equal(AuthReqId, CapturedJob?.AuthReqId);
        Assert.Null(CapturedJob?.Error);
        Assert.Null(CapturedJob?.AccessToken);
    }

    [Fact]
    public async Task DenyAsync_WhenPendingPush_DeliversTheAccessDeniedErrorPayload()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupClientEndpoint(mockClient, Endpoint);
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Push
            )
        );

        SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Push
            )
        );

        SetupDispatch();

        var result = await Service.DenyAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CancellationToken.None
        );

        Assert.True(result);
        MockJobDispatcher.Verify();
        Assert.Equal(OpenIdConstants.BackchannelTokenDeliveryModes.Push, CapturedJob?.DeliveryMode);
        Assert.Equal(AuthReqId, CapturedJob?.AuthReqId);
        Assert.Equal(OpenIdProtocol.ErrorCodes.AccessDenied, CapturedJob?.Error);
        Assert.Null(CapturedJob?.AccessToken);
    }

    [Fact]
    public async Task DenyAsync_WhenAnotherRequestDecidedFirst_NotifiesNoClient()
    {
        var (mockContext, mockClient) = CreateScaffold();
        SetupLookup(
            CreateGrant(
                DeviceAuthorizationStatus.Pending,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            )
        );

        SetupDecision(
            CreateGrant(
                DeviceAuthorizationStatus.Denied,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping
            ),
            decided: false
        );

        var result = await Service.DenyAsync(
            mockContext.Object,
            mockClient.Object,
            AuthReqId,
            CancellationToken.None
        );

        Assert.False(result);
    }
}
