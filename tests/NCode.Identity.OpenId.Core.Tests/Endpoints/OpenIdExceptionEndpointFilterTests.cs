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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints;

public class OpenIdExceptionEndpointFilterTests : BaseTests
{
    private static HttpContext CreateHttpContext(
        IAuditEventRecorder recorder,
        OpenIdContext openIdContext
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddLogging();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        httpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature => feature.OpenIdContext == openIdContext)
        );
        return httpContext;
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorizationResultCarriesError_AuditsOnce()
    {
        var mockRecorder = CreateLooseMock<IAuditEventRecorder>();
        var mockContext = CreateLooseMock<OpenIdContext>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockError.SetupGet(x => x.State).Returns("state-1");

        var httpContext = CreateHttpContext(mockRecorder.Object, mockContext.Object);
        var filterContext = EndpointFilterInvocationContext.Create(httpContext);

        // A redirect-delivered authorization error is carried by AuthorizationResult (ISupportOpenIdError),
        // not an OpenIdResult; the funnel must still audit it from the returned result.
        var authorizationResult = new AuthorizationResult(
            new Uri("https://rp.example/callback"),
            "query",
            mockError.Object
        );

        var filter = new OpenIdExceptionEndpointFilter();
        var result = await filter.InvokeAsync(
            filterContext,
            _ => ValueTask.FromResult<object?>(authorizationResult)
        );

        Assert.Same(authorizationResult, result);
        mockRecorder.Verify(
            x =>
                x.RecordOpenIdErrorAsync(
                    mockContext.Object,
                    mockError.Object,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task InvokeAsync_WhenResultHasNoError_DoesNotAudit()
    {
        var mockRecorder = CreateLooseMock<IAuditEventRecorder>();
        var mockContext = CreateLooseMock<OpenIdContext>();

        var httpContext = CreateHttpContext(mockRecorder.Object, mockContext.Object);
        var filterContext = EndpointFilterInvocationContext.Create(httpContext);

        var okResult = Mock.Of<IResult>();

        var filter = new OpenIdExceptionEndpointFilter();
        var result = await filter.InvokeAsync(
            filterContext,
            _ => ValueTask.FromResult<object?>(okResult)
        );

        Assert.Same(okResult, result);
        mockRecorder.Verify(
            x =>
                x.RecordOpenIdErrorAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<IOpenIdError>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
