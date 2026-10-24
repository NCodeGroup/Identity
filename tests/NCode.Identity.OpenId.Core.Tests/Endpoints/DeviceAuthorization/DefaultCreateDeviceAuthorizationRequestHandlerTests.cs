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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.DeviceAuthorization;

public class DefaultCreateDeviceAuthorizationRequestHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";

    // The handler anchors the published verification_uri to the tenant's base address rather than the request.
    private static readonly UriDescriptor BaseAddress = new()
    {
        Scheme = "https",
        Host = new HostString("server"),
        Path = PathString.Empty,
    };

    private static readonly DateTimeOffset UtcNow = DateTimeOffset.Parse(
        "2026-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<LinkGenerator> MockLinkGenerator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<IUserCodeGenerator> MockUserCodeGenerator { get; }
    private DefaultCreateDeviceAuthorizationRequestHandler Handler { get; }

    public DefaultCreateDeviceAuthorizationRequestHandlerTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockLinkGenerator = CreateStrictMock<LinkGenerator>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        MockUserCodeGenerator = CreateStrictMock<IUserCodeGenerator>();
        Handler = new DefaultCreateDeviceAuthorizationRequestHandler(
            MockTimeProvider.Object,
            MockLinkGenerator.Object,
            MockCryptoService.Object,
            MockPersistedGrantService.Object,
            MockUserCodeGenerator.Object
        );
    }

    [Fact]
    public async Task HandleAsync_WhenValid_PersistsBothGrantsAndReturnsResult()
    {
        var httpContext = new DefaultHttpContext();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        var mockRequest = CreateStrictMock<IDeviceAuthorizationRequest>();

        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        mockTenant.SetupGet(x => x.BaseAddress).Returns(BaseAddress).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns("client-1").Verifiable();

        mockRequest.SetupGet(x => x.Scopes).Returns(["api", "openid"]);

        mockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.UserCodeLength)).Returns(8);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DeviceCodeLifetime))
            .Returns(TimeSpan.FromMinutes(5));
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DeviceCodePollingInterval))
            .Returns(TimeSpan.FromSeconds(5));

        MockCryptoService
            .Setup(x => x.GenerateKey(32, BinaryEncodings.Base64Url))
            .Returns("device-code-value");
        MockUserCodeGenerator.Setup(x => x.Generate(8)).Returns("ABCD-1234");
        MockUserCodeGenerator.Setup(x => x.Normalize("ABCD-1234")).Returns("ABCD1234");
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.DeviceCode,
                    "device-code-value"
                )
            )
            .Returns(
                new PersistedGrantId
                {
                    TenantId = TenantId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.DeviceCode,
                    GrantKey = "device-code-value",
                }
            )
            .Verifiable();
        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.DeviceUserCode,
                    "ABCD1234"
                )
            )
            .Returns(
                new PersistedGrantId
                {
                    TenantId = TenantId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.DeviceUserCode,
                    GrantKey = "ABCD1234",
                }
            )
            .Verifiable();

        DeviceCodeGrant? capturedDeviceCode = null;
        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<PersistedGrant<DeviceCodeGrant>>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    PersistedGrant<DeviceCodeGrant> grant,
                    DateTimeOffset _,
                    TimeSpan? _,
                    CancellationToken _
                ) => capturedDeviceCode = grant.Payload
            )
            .Returns(new ValueTask<string>("id"))
            .Verifiable();

        DeviceUserCodeGrant? capturedUserCode = null;
        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<PersistedGrant<DeviceUserCodeGrant>>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    PersistedGrant<DeviceUserCodeGrant> grant,
                    DateTimeOffset _,
                    TimeSpan? _,
                    CancellationToken _
                ) => capturedUserCode = grant.Payload
            )
            .Returns(new ValueTask<string>("id"))
            .Verifiable();

        MockLinkGenerator
            .Setup(x =>
                x.GetUriByAddress(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<RouteValueDictionary>(),
                    It.IsAny<RouteValueDictionary?>(),
                    It.IsAny<string?>(),
                    It.IsAny<HostString?>(),
                    It.IsAny<PathString?>(),
                    It.IsAny<FragmentString>(),
                    It.IsAny<LinkOptions?>()
                )
            )
            .Returns("https://server/device/verify");

        var result = await Handler.HandleAsync(
            new CreateDeviceAuthorizationRequestCommand(
                mockContext.Object,
                mockClient.Object,
                mockRequest.Object
            ),
            CancellationToken.None
        );

        Assert.Equal("device-code-value", result.DeviceCode);
        Assert.Equal("ABCD-1234", result.UserCode);
        Assert.Equal("https://server/device/verify", result.VerificationUri);
        Assert.Equal("https://server/device/verify", result.VerificationUriComplete);
        Assert.Equal(300, result.ExpiresIn);
        Assert.Equal(5, result.Interval);

        Assert.NotNull(capturedDeviceCode);
        var deviceCodePayload = capturedDeviceCode.Value;
        Assert.Equal(DeviceAuthorizationStatus.Pending, deviceCodePayload.Status);
        Assert.Equal(new[] { "api", "openid" }, deviceCodePayload.Scopes);
        Assert.Null(deviceCodePayload.SubjectAuthentication);
        Assert.Equal("device-code-value", capturedUserCode?.DeviceCode);

        MockPersistedGrantService.Verify();
    }

    [Fact]
    public async Task HandleAsync_WhenScopesNull_PersistsEmptyScopes()
    {
        var httpContext = new DefaultHttpContext();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        var mockRequest = CreateStrictMock<IDeviceAuthorizationRequest>();

        mockContext.SetupGet(x => x.Http).Returns(httpContext);
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        mockTenant.SetupGet(x => x.BaseAddress).Returns(BaseAddress);
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);
        mockClient.SetupGet(x => x.ClientId).Returns("client-1");

        mockRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null);

        mockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.UserCodeLength)).Returns(8);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DeviceCodeLifetime))
            .Returns(TimeSpan.FromMinutes(5));
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DeviceCodePollingInterval))
            .Returns(TimeSpan.FromSeconds(5));

        MockCryptoService
            .Setup(x => x.GenerateKey(32, BinaryEncodings.Base64Url))
            .Returns("device-code-value");
        MockUserCodeGenerator.Setup(x => x.Generate(8)).Returns("ABCD-1234");
        MockUserCodeGenerator.Setup(x => x.Normalize("ABCD-1234")).Returns("ABCD1234");
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow);

        MockPersistedGrantService
            .Setup(x => x.CreateGrantId(TenantId, It.IsAny<string>(), It.IsAny<string>()))
            .Returns(
                new PersistedGrantId
                {
                    TenantId = TenantId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.DeviceCode,
                    GrantKey = "key",
                }
            );

        DeviceCodeGrant? capturedDeviceCode = null;
        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<PersistedGrant<DeviceCodeGrant>>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    PersistedGrant<DeviceCodeGrant> grant,
                    DateTimeOffset _,
                    TimeSpan? _,
                    CancellationToken _
                ) => capturedDeviceCode = grant.Payload
            )
            .Returns(new ValueTask<string>("id"));
        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<PersistedGrant<DeviceUserCodeGrant>>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<string>("id"));

        MockLinkGenerator
            .Setup(x =>
                x.GetUriByAddress(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<RouteValueDictionary>(),
                    It.IsAny<RouteValueDictionary?>(),
                    It.IsAny<string?>(),
                    It.IsAny<HostString?>(),
                    It.IsAny<PathString?>(),
                    It.IsAny<FragmentString>(),
                    It.IsAny<LinkOptions?>()
                )
            )
            .Returns("https://server/device/verify");

        await Handler.HandleAsync(
            new CreateDeviceAuthorizationRequestCommand(
                mockContext.Object,
                mockClient.Object,
                mockRequest.Object
            ),
            CancellationToken.None
        );

        Assert.NotNull(capturedDeviceCode);
        Assert.Empty(capturedDeviceCode.Value.Scopes);
    }

    [Fact]
    public async Task HandleAsync_WhenVerificationUriMissing_Throws()
    {
        var httpContext = new DefaultHttpContext();
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        var mockRequest = CreateStrictMock<IDeviceAuthorizationRequest>();

        mockContext.SetupGet(x => x.Http).Returns(httpContext);
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        mockTenant.SetupGet(x => x.BaseAddress).Returns(BaseAddress);
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);
        mockClient.SetupGet(x => x.ClientId).Returns("client-1");

        mockRequest.SetupGet(x => x.Scopes).Returns(["api"]);

        mockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.UserCodeLength)).Returns(8);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DeviceCodeLifetime))
            .Returns(TimeSpan.FromMinutes(5));
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DeviceCodePollingInterval))
            .Returns(TimeSpan.FromSeconds(5));

        MockCryptoService
            .Setup(x => x.GenerateKey(32, BinaryEncodings.Base64Url))
            .Returns("device-code-value");
        MockUserCodeGenerator.Setup(x => x.Generate(8)).Returns("ABCD-1234");
        MockUserCodeGenerator.Setup(x => x.Normalize("ABCD-1234")).Returns("ABCD1234");
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow);

        MockPersistedGrantService
            .Setup(x => x.CreateGrantId(TenantId, It.IsAny<string>(), It.IsAny<string>()))
            .Returns(
                new PersistedGrantId
                {
                    TenantId = TenantId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.DeviceCode,
                    GrantKey = "key",
                }
            );
        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<PersistedGrant<DeviceCodeGrant>>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<string>("id"));
        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<PersistedGrant<DeviceUserCodeGrant>>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<string>("id"));

        MockLinkGenerator
            .Setup(x =>
                x.GetUriByAddress(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<RouteValueDictionary>(),
                    It.IsAny<RouteValueDictionary?>(),
                    It.IsAny<string?>(),
                    It.IsAny<HostString?>(),
                    It.IsAny<PathString?>(),
                    It.IsAny<FragmentString>(),
                    It.IsAny<LinkOptions?>()
                )
            )
            .Returns((string?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler
                .HandleAsync(
                    new CreateDeviceAuthorizationRequestCommand(
                        mockContext.Object,
                        mockClient.Object,
                        mockRequest.Object
                    ),
                    CancellationToken.None
                )
                .AsTask()
        );
    }
}
