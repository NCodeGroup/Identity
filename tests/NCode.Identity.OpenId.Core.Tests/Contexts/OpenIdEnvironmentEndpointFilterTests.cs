// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Contexts;

public class OpenIdEnvironmentEndpointFilterTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private Mock<IOpenIdContextFactory> MockContextFactory { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<IOpenIdEnvironmentProvider> MockEnvironmentProvider { get; }
    private Mock<IOpenIdExceptionTranslator> MockTranslator { get; }
    private Mock<IMediator> MockMediator { get; }
    private DefaultHttpContext HttpContext { get; }
    private OpenIdEnvironmentEndpointFilter Filter { get; }

    public OpenIdEnvironmentEndpointFilterTests()
    {
        MockContextFactory = CreateStrictMock<IOpenIdContextFactory>();
        MockAmbientTenantAccessor = CreateStrictMock<IAmbientTenantAccessor>();
        MockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        MockTranslator = CreateStrictMock<IOpenIdExceptionTranslator>();
        MockMediator = CreateStrictMock<IMediator>();

        HttpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(MockMediator.Object)
                .BuildServiceProvider(),
        };

        Filter = new OpenIdEnvironmentEndpointFilter(
            MockContextFactory.Object,
            MockAmbientTenantAccessor.Object,
            MockEnvironmentProvider.Object,
            [MockTranslator.Object]
        );
    }

    #region Scaffolding

    private void SetupContextFailure(Exception exception) =>
        MockContextFactory
            .Setup(x =>
                x.CreateForRequestAsync(
                    HttpContext,
                    MockMediator.Object,
                    HttpContext.RequestAborted
                )
            )
            .ThrowsAsync(exception)
            .Verifiable();

    private ValueTask<object?> InvokeAsync(
        EndpointFilterDelegate next,
        CancellationToken cancellationToken
    )
    {
        HttpContext.RequestAborted = cancellationToken;
        return Filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(HttpContext), next);
    }

    #endregion

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_WhenATranslatorAnswersTheExceptionThatCreatesTheContext_ReturnsTheErrorWithoutRunningTheEndpoint()
    {
        var exception = new InvalidOperationException("The root key is unavailable.");
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        IOpenIdError? error = new DefaultOpenIdError(
            mockEnvironment.Object,
            OpenIdProtocol.ErrorCodes.TemporarilyUnavailable
        );
        SetupContextFailure(exception);
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();
        MockTranslator
            .Setup(x => x.TryTranslate(mockEnvironment.Object, exception, out error))
            .Returns(true)
            .Verifiable();
        var endpointRan = false;

        var result = await InvokeAsync(
            _ =>
            {
                endpointRan = true;
                return ValueTask.FromResult<object?>(null);
            },
            CancellationToken.None
        );

        Assert.False(endpointRan);
        var openIdResult = Assert.IsAssignableFrom<IOpenIdResult>(result);
        Assert.Same(error, openIdResult.Response);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoTranslatorAnswersTheExceptionThatCreatesTheContext_Propagates()
    {
        var exception = new InvalidOperationException("The context cannot be created.");
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        IOpenIdError? error = null;
        SetupContextFailure(exception);
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(mockEnvironment.Object).Verifiable();
        MockTranslator
            .Setup(x => x.TryTranslate(mockEnvironment.Object, exception, out error))
            .Returns(false)
            .Verifiable();
        var endpointRan = false;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeAsync(
                _ =>
                {
                    endpointRan = true;
                    return ValueTask.FromResult<object?>(null);
                },
                CancellationToken.None
            )
        );

        Assert.Same(exception, thrown);
        Assert.False(endpointRan);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheContextIsCreated_OpensTheAmbientScopeAndRunsTheEndpoint()
    {
        var expected = new object();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        var mockScope = CreateStrictMock<IDisposable>();
        mockScope.Setup(x => x.Dispose()).Verifiable();
        MockContextFactory
            .Setup(x =>
                x.CreateForRequestAsync(
                    HttpContext,
                    MockMediator.Object,
                    HttpContext.RequestAborted
                )
            )
            .ReturnsAsync(mockContext.Object)
            .Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(mockScope.Object)
            .Verifiable();

        var result = await InvokeAsync(
            _ => ValueTask.FromResult<object?>(expected),
            CancellationToken.None
        );

        Assert.Same(expected, result);
    }

    #endregion
}
