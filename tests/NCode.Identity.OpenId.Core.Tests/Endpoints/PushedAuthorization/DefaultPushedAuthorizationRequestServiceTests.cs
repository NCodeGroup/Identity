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
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization.Logic;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.PushedAuthorization;

public class DefaultPushedAuthorizationRequestServiceTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string Reference = "par-reference-value";

    private static readonly DateTimeOffset UtcNow = DateTimeOffset.Parse(
        "2026-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private DefaultPushedAuthorizationRequestService Service { get; }

    public DefaultPushedAuthorizationRequestServiceTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        Service = new DefaultPushedAuthorizationRequestService(
            MockTimeProvider.Object,
            MockCryptoService.Object,
            MockPersistedGrantService.Object
        );
    }

    private static IRequestValues CreateRequestValues(Dictionary<string, StringValues> values) =>
        new RequestValuesUsingForm(new FormCollection(values));

    #region StoreAsync

    [Fact]
    public async Task StoreAsync_WhenValid_PersistsRequestAndReturnsRequestUri()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();

        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.PushedAuthorizationRequestLifetime))
            .Returns(TimeSpan.FromSeconds(90))
            .Verifiable();

        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        MockCryptoService
            .Setup(x => x.GenerateKey(32, BinaryEncodingType.Base64Url))
            .Returns(Reference)
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.PushedAuthorizationRequest,
                    Reference
                )
            )
            .Returns(
                new PersistedGrantId
                {
                    TenantId = TenantId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.PushedAuthorizationRequest,
                    GrantKey = Reference,
                }
            )
            .Verifiable();

        PushedAuthorizationGrant? capturedPayload = null;
        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<PersistedGrant<PushedAuthorizationGrant>>(),
                    UtcNow,
                    TimeSpan.FromSeconds(90),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    PersistedGrant<PushedAuthorizationGrant> grant,
                    DateTimeOffset _,
                    TimeSpan? _,
                    CancellationToken _
                ) => capturedPayload = grant.Payload
            )
            .ReturnsAsync(Reference)
            .Verifiable();

        var requestValues = CreateRequestValues(
            new Dictionary<string, StringValues>
            {
                ["client_id"] = ClientId,
                ["response_type"] = "code",
                ["scope"] = "openid",
                ["client_secret"] = "super-secret",
            }
        );

        var result = await Service.StoreAsync(
            mockContext.Object,
            mockClient.Object,
            requestValues,
            CancellationToken.None
        );

        Assert.Equal(
            OpenIdConstants.PushedAuthorizationRequestUriPrefix + Reference,
            result.RequestUri
        );
        Assert.Equal(90, result.ExpiresIn);

        Assert.NotNull(capturedPayload);
        Assert.Equal(ClientId, capturedPayload.Value.ClientId);
        Assert.True(capturedPayload.Value.Parameters.ContainsKey("response_type"));
        Assert.True(capturedPayload.Value.Parameters.ContainsKey("scope"));
        // Client-authentication secrets must never be persisted as part of the pushed request.
        Assert.False(capturedPayload.Value.Parameters.ContainsKey("client_secret"));
    }

    #endregion

    #region ConsumeAsync

    [Fact]
    public async Task ConsumeAsync_WhenValid_ReturnsReplayedParameters()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.PushedAuthorizationRequest,
                    Reference
                )
            )
            .Returns(
                new PersistedGrantId
                {
                    TenantId = TenantId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.PushedAuthorizationRequest,
                    GrantKey = Reference,
                }
            )
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.ConsumeOnceOrDefault<PushedAuthorizationGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                ValueTask.FromResult<PersistedGrant<PushedAuthorizationGrant>?>(
                    new PersistedGrant<PushedAuthorizationGrant>
                    {
                        Status = PersistedGrantStatus.Active,
                        TenantId = TenantId,
                        ClientId = ClientId,
                        SubjectId = null,
                        SessionId = null,
                        Payload = new PushedAuthorizationGrant
                        {
                            ClientId = ClientId,
                            Parameters = new Dictionary<string, IReadOnlyList<string>>
                            {
                                ["response_type"] = ["code"],
                                ["scope"] = ["openid"],
                            },
                        },
                    }
                )
            )
            .Verifiable();

        var requestUri = OpenIdConstants.PushedAuthorizationRequestUriPrefix + Reference;

        var result = await Service.ConsumeAsync(
            mockContext.Object,
            mockClient.Object,
            requestUri,
            CancellationToken.None
        );

        Assert.Equal(RequestValuesSourceTypes.PushedAuthorizationRequest, result.SourceType);
        Assert.True(result.TryGetValue("response_type", out var responseType));
        Assert.Equal("code", responseType.ToString());
        Assert.True(result.TryGetValue("scope", out var scope));
        Assert.Equal("openid", scope.ToString());
    }

    [Fact]
    public async Task ConsumeAsync_WhenRequestUriNotPushed_ThrowsInvalidRequestUri()
    {
        var (mockContext, capturedCode) = CreateErrorScaffold();
        var mockClient = CreateStrictMock<OpenIdClient>();

        await Assert.ThrowsAsync<OpenIdException>(() =>
            Service
                .ConsumeAsync(
                    mockContext.Object,
                    mockClient.Object,
                    "https://client.example/request.jwt",
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidRequestUri, capturedCode.Value);
    }

    [Fact]
    public async Task ConsumeAsync_WhenGrantNotFound_ThrowsInvalidRequestUri()
    {
        var (mockContext, capturedCode) = CreateErrorScaffold();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.PushedAuthorizationRequest,
                    Reference
                )
            )
            .Returns(
                new PersistedGrantId
                {
                    TenantId = TenantId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.PushedAuthorizationRequest,
                    GrantKey = Reference,
                }
            );

        MockPersistedGrantService
            .Setup(x =>
                x.ConsumeOnceOrDefault<PushedAuthorizationGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PersistedGrant<PushedAuthorizationGrant>?)null);

        await Assert.ThrowsAsync<OpenIdException>(() =>
            Service
                .ConsumeAsync(
                    mockContext.Object,
                    mockClient.Object,
                    OpenIdConstants.PushedAuthorizationRequestUriPrefix + Reference,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidRequestUri, capturedCode.Value);
    }

    [Fact]
    public async Task ConsumeAsync_WhenClientMismatch_ThrowsInvalidRequestUri()
    {
        var (mockContext, capturedCode) = CreateErrorScaffold();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        mockClient.SetupGet(x => x.ClientId).Returns("other-client");

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.PushedAuthorizationRequest,
                    Reference
                )
            )
            .Returns(
                new PersistedGrantId
                {
                    TenantId = TenantId,
                    GrantType = OpenIdConstants.PersistedGrantTypes.PushedAuthorizationRequest,
                    GrantKey = Reference,
                }
            );

        MockPersistedGrantService
            .Setup(x =>
                x.ConsumeOnceOrDefault<PushedAuthorizationGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                ValueTask.FromResult<PersistedGrant<PushedAuthorizationGrant>?>(
                    new PersistedGrant<PushedAuthorizationGrant>
                    {
                        Status = PersistedGrantStatus.Active,
                        TenantId = TenantId,
                        ClientId = ClientId,
                        SubjectId = null,
                        SessionId = null,
                        Payload = new PushedAuthorizationGrant
                        {
                            ClientId = ClientId,
                            Parameters = new Dictionary<string, IReadOnlyList<string>>
                            {
                                ["scope"] = ["openid"],
                            },
                        },
                    }
                )
            );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            Service
                .ConsumeAsync(
                    mockContext.Object,
                    mockClient.Object,
                    OpenIdConstants.PushedAuthorizationRequestUriPrefix + Reference,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidRequestUri, capturedCode.Value);
    }

    private (Mock<OpenIdContext> context, StrongBox<string?> capturedCode) CreateErrorScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        var capturedCode = new StrongBox<string?>(null);

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => capturedCode.Value = code)
            .Returns(mockError.Object);

        return (mockContext, capturedCode);
    }

    #endregion
}
