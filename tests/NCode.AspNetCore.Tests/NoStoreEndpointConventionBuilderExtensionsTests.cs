// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Moq;
using Xunit;

namespace NCode.AspNetCore.Tests;

public class NoStoreEndpointConventionBuilderExtensionsTests : BaseTests
{
    #region WithNoStore Tests

    [Fact]
    public void WithNoStore_Always_AddsAnEndpointFilter()
    {
        var routePattern = RoutePatternFactory.Parse("/");
        var endpointBuilder = new RouteEndpointBuilder(
            requestDelegate: null,
            routePattern,
            order: 0
        );
        var mockBuilder = CreateStrictMock<IEndpointConventionBuilder>();
        mockBuilder
            .Setup(x => x.Add(It.IsAny<Action<EndpointBuilder>>()))
            .Callback((Action<EndpointBuilder> convention) => convention(endpointBuilder))
            .Verifiable();

        var result = mockBuilder.Object.WithNoStore();

        Assert.Same(mockBuilder.Object, result);
        Assert.Single(endpointBuilder.FilterFactories);
    }

    [Fact]
    public void WithNoStore_WhenTheBuilderIsNull_ThrowsArgumentNullException()
    {
        IEndpointConventionBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => builder.WithNoStore());

        Assert.Equal("builder", exception.ParamName);
    }

    #endregion
}
