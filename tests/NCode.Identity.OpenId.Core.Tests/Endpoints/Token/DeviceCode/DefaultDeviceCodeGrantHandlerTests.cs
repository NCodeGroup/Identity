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

using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.DeviceCode;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.DeviceCode;

public class DefaultDeviceCodeGrantHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string DeviceCodeValue = "device-code";

    private static readonly DateTimeOffset UtcNow = DateTimeOffset.Parse(
        "2026-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<ITokenService> MockTokenService { get; }
    private DefaultDeviceCodeGrantHandler Handler { get; }

    private string? CapturedErrorCode { get; set; }

    public DefaultDeviceCodeGrantHandlerTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        MockTokenService = CreateStrictMock<ITokenService>();
        Handler = new DefaultDeviceCodeGrantHandler(
            MockTimeProvider.Object,
            MockPersistedGrantService.Object,
            MockTokenService.Object
        );
    }

    #region Scaffolding

    private static PersistedGrantId CreateGrantId() =>
        new()
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.DeviceCode,
            GrantKey = DeviceCodeValue,
        };

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private static SecurityToken CreateSecurityToken(string tokenValue) =>
        new()
        {
            TokenType = OpenIdConstants.TokenTypes.Bearer,
            TokenValue = tokenValue,
            TokenLifetime = new TimePeriod { StartTime = UtcNow, EndTime = UtcNow.AddHours(1) },
        };

    private static PersistedGrant<DeviceCodeGrant> CreateGrant(
        string status,
        DateTimeOffset? lastPolledWhen = null,
        SubjectAuthentication? subjectAuthentication = null
    ) =>
        new()
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            Payload = new DeviceCodeGrant
            {
                ClientId = "client-1",
                Scopes = ["api"],
                Status = status,
                SubjectAuthentication = subjectAuthentication,
                LastPolledWhen = lastPolledWhen,
            },
        };

    private (
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client,
        Mock<IReadOnlySettingCollection> settings,
        Mock<ITokenRequest> tokenRequest,
        Mock<IOpenIdError> error
    ) CreateScaffold(string? deviceCode = DeviceCodeValue)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();

        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => CapturedErrorCode = code)
            .Returns(mockError.Object);

        mockTokenRequest.SetupGet(x => x.DeviceCode).Returns(deviceCode).Verifiable();

        return (mockContext, mockClient, mockSettings, mockTokenRequest, mockError);
    }

    private void SetupLookup(PersistedGrant<DeviceCodeGrant>? grant)
    {
        MockPersistedGrantService
            .Setup(x => x.CreateGrantId(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(CreateGrantId())
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<DeviceCodeGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PersistedGrant<DeviceCodeGrant>?>(grant))
            .Verifiable();
    }

    #endregion

    #region GrantTypes

    [Fact]
    public void GrantTypes_Always_ContainsDeviceCode()
    {
        Assert.Contains(OpenIdConstants.GrantTypes.DeviceCode, Handler.GrantTypes);
    }

    #endregion

    #region HandleAsync

    [Fact]
    public async Task HandleAsync_WhenDeviceCodeMissing_ReturnsMissingParameterError()
    {
        var (mockContext, mockClient, _, mockTokenRequest, mockError) = CreateScaffold(
            deviceCode: null
        );

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantNotFound_ReturnsInvalidGrantError()
    {
        var (mockContext, mockClient, _, mockTokenRequest, mockError) = CreateScaffold();
        SetupLookup(null);

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidGrant, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenDenied_ReturnsAccessDeniedError()
    {
        var (mockContext, mockClient, _, mockTokenRequest, mockError) = CreateScaffold();
        SetupLookup(CreateGrant(DeviceAuthorizationStatus.Denied));

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
        Assert.Equal(OpenIdConstants.ErrorCodes.AccessDenied, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenPendingFirstPoll_ReturnsAuthorizationPending()
    {
        var (mockContext, mockClient, mockSettings, mockTokenRequest, mockError) = CreateScaffold();
        SetupLookup(CreateGrant(DeviceAuthorizationStatus.Pending));

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DeviceCodePollingInterval))
            .Returns(TimeSpan.FromSeconds(5))
            .Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<DeviceCodeGrant>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<bool>(true))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
        Assert.Equal(OpenIdConstants.ErrorCodes.AuthorizationPending, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenPendingPolledTooFast_ReturnsSlowDown()
    {
        var (mockContext, mockClient, mockSettings, mockTokenRequest, mockError) = CreateScaffold();
        SetupLookup(CreateGrant(DeviceAuthorizationStatus.Pending, lastPolledWhen: UtcNow));

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DeviceCodePollingInterval))
            .Returns(TimeSpan.FromSeconds(5))
            .Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<DeviceCodeGrant>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<bool>(true))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
        Assert.Equal(OpenIdConstants.ErrorCodes.SlowDown, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenApprovedAlreadyConsumed_ReturnsInvalidGrant()
    {
        var (mockContext, mockClient, _, mockTokenRequest, mockError) = CreateScaffold();
        SetupLookup(CreateGrant(DeviceAuthorizationStatus.Approved));

        MockPersistedGrantService
            .Setup(x =>
                x.ConsumeOnceOrDefault<DeviceCodeGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PersistedGrant<DeviceCodeGrant>?>(
                    (PersistedGrant<DeviceCodeGrant>?)null
                )
            )
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidGrant, CapturedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenApproved_ReturnsTokenResponseWithAccessToken()
    {
        var (mockContext, mockClient, _, mockTokenRequest, _) = CreateScaffold();
        SetupLookup(CreateGrant(DeviceAuthorizationStatus.Approved));

        MockPersistedGrantService
            .Setup(x =>
                x.ConsumeOnceOrDefault<DeviceCodeGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PersistedGrant<DeviceCodeGrant>?>(
                    CreateGrant(
                        DeviceAuthorizationStatus.Approved,
                        subjectAuthentication: CreateSubjectAuthentication()
                    )
                )
            )
            .Verifiable();

        mockContext
            .SetupGet(x => x.Environment)
            .Returns(CreateStrictMock<OpenIdEnvironment>().Object)
            .Verifiable();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdConstants.GrantTypes.DeviceCode)
            .Verifiable();

        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        MockTokenService
            .Setup(x =>
                x.CreateAccessTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken>(CreateSecurityToken("access-token-value")))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("access-token-value", response.AccessToken);
        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, response.TokenType);
        Assert.Equal(["api"], response.Scopes);
    }

    #endregion
}
