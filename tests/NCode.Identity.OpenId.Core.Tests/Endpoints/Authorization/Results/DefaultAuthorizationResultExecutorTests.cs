#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Core.Tests.Metrics;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Metrics;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization.Results;

public class DefaultAuthorizationResultExecutorTests : BaseTests
{
    private const string NormalName = "code";
    private const string NormalValue = "auth-code";
    private const string InternalName = OpenIdConstants.Parameters.CreatedWhen;
    private const string InternalValue = "2025-01-01T00:00:00Z";

    private static readonly Uri RedirectUri = new("https://client.example/callback");

    private DefaultAuthorizationResultExecutor Executor { get; } = new();

    #region Scaffolding

    private IParameter CreateSerializedParameter(string value)
    {
        var mockParser = CreateStrictMock<ParameterParser<string>>();
        var knownParameter = new KnownParameter<string>(NormalName, mockParser.Object)
        {
            AllowMissingStringValues = true,
        };
        var descriptor = new ParameterDescriptor(knownParameter);

        mockParser
            .Setup(x =>
                x.GetStringValues(
                    It.IsAny<OpenIdEnvironment>(),
                    It.IsAny<ParameterDescriptor>(),
                    value
                )
            )
            .Returns(new StringValues(value))
            .Verifiable();

        return new Parameter<string>(descriptor, mockParser.Object, value);
    }

    private IParameter CreateInternalParameter()
    {
        // A strict parser with no GetStringValues setup: if the executor ever tries to
        // serialize this internal ($-prefixed) parameter, the strict mock throws.
        var mockParser = CreateStrictMock<ParameterParser<string>>();
        var knownParameter = new KnownParameter<string>(InternalName, mockParser.Object)
        {
            AllowMissingStringValues = true,
            ShouldSerialize = OpenIdCommonParameters.ShouldSerializeAsJsonOnly,
        };
        var descriptor = new ParameterDescriptor(knownParameter);

        return new Parameter<string>(descriptor, mockParser.Object, InternalValue);
    }

    private AuthorizationResult CreateResult(
        string responseMode,
        string normalValue = NormalValue,
        Uri? redirectUri = null
    )
    {
        var parameters = new ParameterCollection(
            [CreateSerializedParameter(normalValue), CreateInternalParameter()],
            cloneParameters: false
        );

        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var mockTicket = CreateLooseMock<IAuthorizationTicket>();
        mockTicket.SetupGet(x => x.State).Returns("state-value");
        mockTicket.SetupGet(x => x.OpenIdEnvironment).Returns(mockEnvironment.Object);
        mockTicket.SetupGet(x => x.Parameters).Returns(parameters);

        return new AuthorizationResult(redirectUri ?? RedirectUri, responseMode, mockTicket.Object);
    }

    private static async Task<string> ExecuteFormPostAsync(
        DefaultAuthorizationResultExecutor executor,
        AuthorizationResult result
    )
    {
        var httpContext = new DefaultHttpContext();
        using var body = new MemoryStream();
        httpContext.Response.Body = body;

        await executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        Assert.Equal("text/html; charset=utf-8", httpContext.Response.ContentType);
        body.Position = 0;
        return await new StreamReader(body).ReadToEndAsync();
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenAnErrorIsRedirected_TagsTheErrorCode()
    {
        var mockError = CreateLooseMock<IOpenIdError>();
        mockError.SetupGet(x => x.Code).Returns(OpenIdConstants.ErrorCodes.AccessDenied);
        mockError
            .SetupGet(x => x.OpenIdEnvironment)
            .Returns(CreateStrictMock<OpenIdEnvironment>().Object);
        mockError
            .SetupGet(x => x.Parameters)
            .Returns(new ParameterCollection([], cloneParameters: false));
        var result = new AuthorizationResult(
            RedirectUri,
            OpenIdConstants.ResponseModes.Query,
            mockError.Object
        );
        var metricsTags = new TestHttpMetricsTagsFeature();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IHttpMetricsTagsFeature>(metricsTags);

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        var tag = Assert.Single(metricsTags.Tags);
        Assert.Equal(OpenIdMetricAttributes.Error, tag.Key);
        Assert.Equal(OpenIdConstants.ErrorCodes.AccessDenied, tag.Value);
        Assert.StartsWith(
            RedirectUri.AbsoluteUri,
            httpContext.Response.Headers.Location.ToString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseModeQuery_OmitsInternalParameters()
    {
        var result = CreateResult(OpenIdConstants.ResponseModes.Query);
        var httpContext = new DefaultHttpContext();

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        var location = httpContext.Response.Headers.Location.ToString();

        Assert.Contains($"{NormalName}={NormalValue}", location, StringComparison.Ordinal);
        Assert.DoesNotContain("created_when", location, StringComparison.Ordinal);
        Assert.DoesNotContain(InternalValue, location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseModeFormPost_OmitsInternalParameters()
    {
        var result = CreateResult(OpenIdConstants.ResponseModes.FormPost);
        var httpContext = new DefaultHttpContext();
        var body = new MemoryStream();
        httpContext.Response.Body = body;

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        body.Position = 0;
        var html = await new StreamReader(body).ReadToEndAsync();

        Assert.Contains(NormalName, html, StringComparison.Ordinal);
        Assert.Contains(NormalValue, html, StringComparison.Ordinal);
        Assert.DoesNotContain("created_when", html, StringComparison.Ordinal);
        Assert.DoesNotContain(InternalValue, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseModeFormPost_HtmlEncodesTheValuesWithoutUrlEncoding()
    {
        const string value = "a b=c/d+e'<f>";
        var result = CreateResult(OpenIdConstants.ResponseModes.FormPost, normalValue: value);

        var html = await ExecuteFormPostAsync(Executor, result);

        Assert.Contains(
            $"<input type='hidden' name='{NormalName}' value='{HtmlEncoder.Default.Encode(value)}'/>",
            html,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("%20", html, StringComparison.Ordinal);
        Assert.DoesNotContain("%3D", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseModeFormPost_HtmlEncodesTheAction()
    {
        var redirectUri = new Uri("https://client.example/callback?x='y'");
        var result = CreateResult(OpenIdConstants.ResponseModes.FormPost, redirectUri: redirectUri);

        var html = await ExecuteFormPostAsync(Executor, result);

        Assert.Contains(
            $"action='{HtmlEncoder.Default.Encode(redirectUri.AbsoluteUri)}'>",
            html,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("x='y'", html, StringComparison.Ordinal);
    }

    #endregion
}
