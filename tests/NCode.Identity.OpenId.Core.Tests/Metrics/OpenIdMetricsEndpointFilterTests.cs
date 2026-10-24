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
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Authentication.Metrics;
using NCode.Identity.OpenId.Metrics;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Metrics;

public sealed class OpenIdMetricsEndpointFilterTests : BaseTests
{
    private const string EndpointName = "token_endpoint";

    private OpenIdMetricsEndpointFilter Filter { get; } = new();

    #region Scaffolding

    private static DefaultHttpContext CreateHttpContext(string? endpointName)
    {
        var httpContext = new DefaultHttpContext();
        var metadata = endpointName is null
            ? new EndpointMetadataCollection()
            : new EndpointMetadataCollection(new EndpointNameMetadata(endpointName));
        httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        return httpContext;
    }

    private static ValueTask<object?> InvokeAsync(
        OpenIdMetricsEndpointFilter filter,
        HttpContext httpContext,
        object? result
    ) =>
        filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult(result)
        );

    #endregion

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_WhenTheEndpointIsNamed_TagsTheRequestWithTheEndpointName()
    {
        var httpContext = CreateHttpContext(EndpointName);
        var tagsFeature = new TestHttpMetricsTagsFeature();
        httpContext.Features.Set<IHttpMetricsTagsFeature>(tagsFeature);
        var expectedResult = new object();

        var result = await InvokeAsync(Filter, httpContext, expectedResult);

        Assert.Same(expectedResult, result);
        var tag = Assert.Single(tagsFeature.Tags);
        Assert.Equal(OpenIdMetricAttributes.Endpoint, tag.Key);
        Assert.Equal(EndpointName, tag.Value);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheEndpointIsUnnamed_AddsNoTag()
    {
        var httpContext = CreateHttpContext(endpointName: null);
        var tagsFeature = new TestHttpMetricsTagsFeature();
        httpContext.Features.Set<IHttpMetricsTagsFeature>(tagsFeature);

        await InvokeAsync(Filter, httpContext, result: null);

        Assert.Empty(tagsFeature.Tags);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoMetricsAreCollected_InvokesTheEndpoint()
    {
        var httpContext = CreateHttpContext(EndpointName);
        var expectedResult = new object();

        var result = await InvokeAsync(Filter, httpContext, expectedResult);

        Assert.Same(expectedResult, result);
    }

    #endregion
}
