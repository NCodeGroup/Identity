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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Core.Tests.Metrics;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Metrics;
using NCode.Identity.OpenId.Results;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Results;

public sealed class DefaultOpenIdResultExecutorTests : BaseTests
{
    private DefaultOpenIdResultExecutor Executor { get; } = new();

    private TestHttpMetricsTagsFeature MetricsTags { get; } = new();

    #region Scaffolding

    private DefaultHttpContext CreateHttpContext(IOpenIdResponse response)
    {
        var mockFormatter = CreateStrictMock<IOpenIdResponseFormatter>();
        mockFormatter
            .SetupGet(x => x.MediaTypes)
            .Returns([OpenIdConstants.MediaTypes.Json])
            .Verifiable();
        mockFormatter.Setup(x => x.CanFormat(response)).Returns(true).Verifiable();
        mockFormatter
            .Setup(x =>
                x.WriteAsync(
                    It.Is<OpenIdResponseFormatterContext>(context => context.Response == response),
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(mockFormatter.Object)
                .BuildServiceProvider(),
        };
        httpContext.Features.Set<IHttpMetricsTagsFeature>(MetricsTags);
        return httpContext;
    }

    private IOpenIdResult CreateResult(IOpenIdResponse response)
    {
        var mockResult = CreateStrictMock<IOpenIdResult>();
        mockResult.SetupGet(x => x.Response).Returns(response).Verifiable();
        return mockResult.Object;
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenTheResponseIsAnError_TagsTheRequestWithItsCode()
    {
        var mockError = CreateLooseMock<IOpenIdError>();
        mockError.SetupGet(x => x.Code).Returns(OpenIdProtocol.ErrorCodes.InvalidGrant);
        var httpContext = CreateHttpContext(mockError.Object);

        await Executor.ExecuteAsync(
            httpContext,
            CreateResult(mockError.Object),
            CancellationToken.None
        );

        Assert.Equal(
            [
                new KeyValuePair<string, object?>(
                    OpenIdMetricAttributes.Error,
                    OpenIdProtocol.ErrorCodes.InvalidGrant
                ),
            ],
            MetricsTags.Tags
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheResponseIsNotAnError_AddsNoTag()
    {
        var response = CreateLooseMock<IOpenIdResponse>().Object;
        var httpContext = CreateHttpContext(response);

        await Executor.ExecuteAsync(httpContext, CreateResult(response), CancellationToken.None);

        Assert.Empty(MetricsTags.Tags);
    }

    #endregion
}
