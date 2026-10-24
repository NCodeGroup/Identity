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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultClientAuthenticationServiceTests : BaseTests
{
    private const string ClientId = "client-1";

    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; }
    private Mock<IRequestRateLimiter> MockRateLimiter { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private int HandlerCount { get; set; }

    public DefaultClientAuthenticationServiceTests()
    {
        MockAuditEventRecorder = CreateStrictMock<IAuditEventRecorder>();
        MockRateLimiter = CreateStrictMock<IRequestRateLimiter>();

        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Returns<string>(code =>
            {
                var mockError = CreateLooseMock<IOpenIdError>();
                mockError.SetupAllProperties();
                mockError.Object.Code = code;
                return mockError.Object;
            });

        MockContext = CreateStrictMock<OpenIdContext>();
        MockContext.SetupGet(x => x.Http).Returns(new DefaultHttpContext()).Verifiable();
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
    }

    #region Scaffolding

    private DefaultClientAuthenticationService CreateService(
        params IClientAuthenticationHandler[] handlers
    ) =>
        new(
            handlers,
            CreateCatalog(handlers.Select(x => x.AuthenticationMethod)),
            MockAuditEventRecorder.Object,
            MockRateLimiter.Object,
            CreateLooseMock<ILogger<DefaultClientAuthenticationService>>().Object
        );

    private static DefaultClientAuthenticationMethodCatalog CreateCatalog(
        IEnumerable<string> names
    ) =>
        new([
            new StaticDataSource<ClientAuthenticationMethodDescriptor>([
                .. names.Select(name => new ClientAuthenticationMethodDescriptor { Name = name }),
            ]),
        ]);

    private IClientAuthenticationHandler CreateHandler(ClientAuthenticationResult result)
    {
        var mockHandler = CreateStrictMock<IClientAuthenticationHandler>();
        mockHandler
            .SetupGet(x => x.AuthenticationMethod)
            .Returns($"method-{++HandlerCount}")
            .Verifiable();
        mockHandler
            .Setup(x => x.AuthenticateClientAsync(MockContext.Object, CancellationToken.None))
            .ReturnsAsync(result)
            .Verifiable();
        return mockHandler.Object;
    }

    private Mock<IReadOnlySettingCollection> CreateSettings(
        string registeredMethod,
        IReadOnlyCollection<string>? ceiling
    )
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns(registeredMethod)
            .Verifiable();
        if (ceiling is not null)
        {
            var permittedMethods = ceiling;
            mockSettings
                .Setup(x =>
                    x.TryGetValue(
                        OpenIdSettingKeys.TokenEndpointAuthMethodsSupported,
                        out permittedMethods
                    )
                )
                .Returns(true)
                .Verifiable();
        }

        return mockSettings;
    }

    private OpenIdClient CreatePublicClient(
        string registeredMethod,
        IReadOnlyCollection<string>? ceiling,
        bool isRefused
    )
    {
        var mockSettings = CreateSettings(registeredMethod, ceiling);
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.IsConfidential).Returns(false).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        if (isRefused)
            mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        return mockClient.Object;
    }

    private OpenIdConfidentialClient CreateConfidentialClient(
        string authenticationMethod,
        string registeredMethod,
        IReadOnlyCollection<string>? ceiling,
        bool isRefused
    )
    {
        var mockSettings = CreateSettings(registeredMethod, ceiling);
        var mockClient = CreateStrictMock<OpenIdConfidentialClient>();
        mockClient.SetupGet(x => x.IsConfidential).Returns(true).Verifiable();
        mockClient
            .SetupGet(x => x.ConfidentialClient)
            .Returns(() => mockClient.Object)
            .Verifiable();
        mockClient.SetupGet(x => x.AuthenticationMethod).Returns(authenticationMethod).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        if (isRefused)
            mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        return mockClient.Object;
    }

    private void SetupClientRateLimitAdmits(OpenIdConfidentialClient client) =>
        MockRateLimiter
            .Setup(x =>
                x.AcquireAsync(
                    RateLimitStages.ClientAuthentication,
                    It.Is<RateLimitContext>(context =>
                        context.OpenIdContext == MockContext.Object && context.Client == client
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(RateLimitDecision.Admitted)
            .Verifiable();

    private void SetupAudit(string authenticationMethod, string reason) =>
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordClientAuthenticationFailedAsync(
                    MockContext.Object,
                    ClientId,
                    authenticationMethod,
                    reason,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private static void AssertError(
        ClientAuthenticationResult result,
        string expectedCode,
        int expectedStatusCode
    )
    {
        Assert.True(result.IsError);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(expectedStatusCode, result.Error.StatusCode);
    }

    #endregion

    #region AuthenticateClientAsync Tests

    [Fact]
    public async Task AuthenticateClientAsync_WhenNoHandlerRecognizesTheRequest_ReturnsUndefined()
    {
        var service = CreateService(CreateHandler(ClientAuthenticationResult.Undefined));

        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenAHandlerFails_ReturnsItsError()
    {
        var mockError = CreateLooseMock<IOpenIdError>();
        var service = CreateService(
            CreateHandler(new ClientAuthenticationResult(mockError.Object))
        );

        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result.Error);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenAPublicClientIsRegisteredAsPublic_ReturnsIt()
    {
        var client = CreatePublicClient(
            OpenIdConstants.ClientAuthenticationMethods.None,
            [OpenIdConstants.ClientAuthenticationMethods.None],
            isRefused: false
        );
        var service = CreateService(CreateHandler(new ClientAuthenticationResult(client)));

        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsPublic);
        Assert.Same(client, result.PublicClient);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenAConfidentialClientAuthenticatesAsPublic_ReturnsInvalidClient()
    {
        // A client registered as confidential never authenticates by presenting only its client_id.
        var client = CreatePublicClient(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            ceiling: null,
            isRefused: true
        );
        var service = CreateService(CreateHandler(new ClientAuthenticationResult(client)));
        SetupAudit(
            OpenIdConstants.ClientAuthenticationMethods.None,
            "authentication_method_not_registered"
        );

        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertError(
            result,
            OpenIdConstants.ErrorCodes.InvalidClient,
            StatusCodes.Status400BadRequest
        );
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheClientUsesItsRegisteredMethod_ReturnsIt()
    {
        var client = CreateConfidentialClient(
            OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
            OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
            [OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt],
            isRefused: false
        );
        SetupClientRateLimitAdmits(client);
        var service = CreateService(CreateHandler(new ClientAuthenticationResult(client)));

        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsConfidential);
        Assert.Same(client, result.ConfidentialClient);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheClientUsesAnotherMethod_ReturnsInvalidClient()
    {
        var client = CreateConfidentialClient(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost,
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            ceiling: null,
            isRefused: true
        );
        var service = CreateService(CreateHandler(new ClientAuthenticationResult(client)));
        SetupAudit(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost,
            "authentication_method_not_registered"
        );

        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertError(
            result,
            OpenIdConstants.ErrorCodes.InvalidClient,
            StatusCodes.Status400BadRequest
        );
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheCeilingExcludesTheRegisteredMethod_ReturnsInvalidClient()
    {
        var client = CreateConfidentialClient(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            [OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt],
            isRefused: true
        );
        var service = CreateService(CreateHandler(new ClientAuthenticationResult(client)));
        SetupAudit(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            "authentication_method_not_permitted"
        );

        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertError(
            result,
            OpenIdConstants.ErrorCodes.InvalidClient,
            StatusCodes.Status400BadRequest
        );
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheCeilingIsEmpty_ReturnsInvalidClient()
    {
        // An empty ceiling (the intersection of disjoint ceilings) permits no method rather than every method.
        var client = CreateConfidentialClient(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            [],
            isRefused: true
        );
        var service = CreateService(CreateHandler(new ClientAuthenticationResult(client)));
        SetupAudit(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            "authentication_method_not_permitted"
        );

        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertError(
            result,
            OpenIdConstants.ErrorCodes.InvalidClient,
            StatusCodes.Status400BadRequest
        );
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenHandlersAuthenticateDifferentClients_ReturnsInvalidRequest()
    {
        var mockFirst = CreateStrictMock<OpenIdClient>();
        mockFirst.SetupGet(x => x.ClientId).Returns("client-a").Verifiable();
        mockFirst.SetupGet(x => x.IsConfidential).Returns(false).Verifiable();
        var mockSecond = CreateStrictMock<OpenIdClient>();
        mockSecond.SetupGet(x => x.ClientId).Returns("client-b").Verifiable();
        mockSecond.SetupGet(x => x.IsConfidential).Returns(false).Verifiable();
        var service = CreateService(
            CreateHandler(new ClientAuthenticationResult(mockFirst.Object)),
            CreateHandler(new ClientAuthenticationResult(mockSecond.Object))
        );

        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertError(
            result,
            OpenIdConstants.ErrorCodes.InvalidRequest,
            StatusCodes.Status400BadRequest
        );
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTwoMethodsAuthenticateTheClient_ReturnsInvalidRequest()
    {
        var mockFirst = CreateStrictMock<OpenIdConfidentialClient>();
        mockFirst.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        mockFirst.SetupGet(x => x.IsConfidential).Returns(true).Verifiable();
        mockFirst.SetupGet(x => x.ConfidentialClient).Returns(() => mockFirst.Object).Verifiable();
        var mockSecond = CreateStrictMock<OpenIdConfidentialClient>();
        mockSecond.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        mockSecond.SetupGet(x => x.IsConfidential).Returns(true).Verifiable();
        mockSecond
            .SetupGet(x => x.ConfidentialClient)
            .Returns(() => mockSecond.Object)
            .Verifiable();
        var service = CreateService(
            CreateHandler(new ClientAuthenticationResult(mockFirst.Object)),
            CreateHandler(new ClientAuthenticationResult(mockSecond.Object))
        );

        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertError(
            result,
            OpenIdConstants.ErrorCodes.InvalidRequest,
            StatusCodes.Status400BadRequest
        );
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenCalledTwiceInOneRequest_AuthenticatesOnce()
    {
        var mockHandler = CreateStrictMock<IClientAuthenticationHandler>();
        mockHandler.SetupGet(x => x.AuthenticationMethod).Returns("method-once").Verifiable();
        mockHandler
            .Setup(x => x.AuthenticateClientAsync(MockContext.Object, CancellationToken.None))
            .ReturnsAsync(ClientAuthenticationResult.Undefined)
            .Verifiable(Times.Once);
        var service = CreateService(mockHandler.Object);

        await service.AuthenticateClientAsync(MockContext.Object, CancellationToken.None);
        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenAPublicClientIsAuthenticated_DoesNotCountItsRateLimit()
    {
        var client = CreatePublicClient(
            OpenIdConstants.ClientAuthenticationMethods.None,
            [OpenIdConstants.ClientAuthenticationMethods.None],
            isRefused: false
        );
        var service = CreateService(CreateHandler(new ClientAuthenticationResult(client)));

        // The strict rate limiter has no setup, so counting the public client would throw.
        await service.AuthenticateClientAsync(MockContext.Object, CancellationToken.None);
    }

    [Fact]
    public async Task AuthenticateClientAsync_WhenTheClientIsOverItsRateLimit_ReturnsTooManyRequests()
    {
        var client = CreateConfidentialClient(
            OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
            OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
            [OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt],
            isRefused: false
        );
        MockRateLimiter
            .Setup(x =>
                x.AcquireAsync(
                    RateLimitStages.ClientAuthentication,
                    It.Is<RateLimitContext>(context =>
                        context.OpenIdContext == MockContext.Object && context.Client == client
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new RateLimitDecision(TimeSpan.FromSeconds(1.5)))
            .Verifiable(Times.Once);
        var service = CreateService(CreateHandler(new ClientAuthenticationResult(client)));

        await service.AuthenticateClientAsync(MockContext.Object, CancellationToken.None);
        var result = await service.AuthenticateClientAsync(
            MockContext.Object,
            CancellationToken.None
        );

        AssertError(
            result,
            OpenIdConstants.ErrorCodes.TemporarilyUnavailable,
            StatusCodes.Status429TooManyRequests
        );
    }

    #endregion

    private sealed class StaticDataSource<T>(IEnumerable<T> items) : ICollectionDataSource<T>
    {
        public IEnumerable<T> Collection { get; } = [.. items];

        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }
}
