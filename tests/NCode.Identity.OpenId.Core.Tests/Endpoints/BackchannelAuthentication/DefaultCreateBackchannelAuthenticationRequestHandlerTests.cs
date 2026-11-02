// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.BackchannelAuthentication;

public class DefaultCreateBackchannelAuthenticationRequestHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private static readonly DateTimeOffset UtcNow = DateTimeOffset.Parse(
        "2026-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private FakeTimeProvider TimeProvider { get; } = new(UtcNow);
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<ICibaNotificationService> MockNotificationService { get; }
    private DefaultCreateBackchannelAuthenticationRequestHandler Handler { get; }

    public DefaultCreateBackchannelAuthenticationRequestHandlerTests()
    {
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        MockNotificationService = CreateStrictMock<ICibaNotificationService>();
        Handler = new DefaultCreateBackchannelAuthenticationRequestHandler(
            TimeProvider,
            MockCryptoService.Object,
            MockPersistedGrantService.Object,
            MockNotificationService.Object
        );
    }

    private static SubjectAuthentication CreateSubject() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    [Fact]
    public async Task HandleAsync_WhenValid_PersistsNotifiesAndReturnsResult()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        var mockRequest = CreateStrictMock<IBackchannelAuthenticationRequest>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns("client-1").Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.BackchannelTokenDeliveryMode))
            .Returns(OpenIdProtocol.BackchannelTokenDeliveryModes.Poll)
            .Verifiable();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.CibaRequestLifetime))
            .Returns(TimeSpan.FromMinutes(10))
            .Verifiable();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.CibaPollingInterval))
            .Returns(TimeSpan.FromSeconds(5))
            .Verifiable();

        mockRequest.SetupGet(x => x.RequestedExpiry).Returns((string?)null).Verifiable();
        mockRequest.SetupGet(x => x.Scopes).Returns(["api"]).Verifiable();
        mockRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockRequest.SetupGet(x => x.ClientNotificationToken).Returns((string?)null).Verifiable();
        mockRequest.SetupGet(x => x.BindingMessage).Returns((string?)null).Verifiable();

        MockCryptoService
            .Setup(x => x.GenerateKey(32, BinaryEncodings.Base64Url))
            .Returns("auth-req-id-value")
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.Ciba,
                    "auth-req-id-value"
                )
            )
            .Returns(
                new PersistedGrantId
                {
                    TenantId = TenantId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.Ciba,
                    GrantKey = "auth-req-id-value",
                }
            )
            .Verifiable();

        CibaGrant? captured = null;
        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<PersistedGrant<CibaGrant>>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    PersistedGrant<CibaGrant> grant,
                    DateTimeOffset _,
                    TimeSpan? _,
                    CancellationToken _
                ) => captured = grant.Payload
            )
            .Returns(new ValueTask<string>("id"))
            .Verifiable();

        MockNotificationService
            .Setup(x =>
                x.NotifyAuthenticationDeviceAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    "auth-req-id-value",
                    It.IsAny<CibaGrant>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.HandleAsync(
            new CreateBackchannelAuthenticationRequestCommand(
                mockContext.Object,
                mockClient.Object,
                mockRequest.Object,
                CreateSubject()
            ),
            CancellationToken.None
        );

        Assert.Equal("auth-req-id-value", result.AuthReqId);
        Assert.Equal(600, result.ExpiresIn);
        Assert.Equal(5, result.Interval);
        Assert.Equal(DeviceAuthorizationStatus.Pending, captured?.Status);
        Assert.Equal("subject-id", captured?.SubjectAuthentication?.PrincipalId);
    }
}
