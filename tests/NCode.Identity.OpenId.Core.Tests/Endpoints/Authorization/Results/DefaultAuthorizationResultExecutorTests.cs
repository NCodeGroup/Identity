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

using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Moq;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Contexts;
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

    private const string RedirectUri = "https://client.example/callback";

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
        IAuthorizationResponseModeHandler handler,
        string normalValue = NormalValue,
        string? redirectUri = null
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

        return new AuthorizationResult(
            CreateLooseMock<OpenIdContext>().Object,
            CreateLooseMock<OpenIdClient>().Object,
            new AuthorizationResponseDelivery(redirectUri ?? RedirectUri, handler, State: null),
            mockTicket.Object
        );
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
        return await new StreamReader(body, SecureEncoding.UTF8).ReadToEndAsync();
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenTheResultIsDelivered_DelegatesToTheDeliverysHandler()
    {
        var ticket = CreateLooseMock<IAuthorizationTicket>().Object;
        var mockHandler = CreateStrictMock<IAuthorizationResponseModeHandler>();
        var result = new AuthorizationResult(
            CreateLooseMock<OpenIdContext>().Object,
            CreateLooseMock<OpenIdClient>().Object,
            new AuthorizationResponseDelivery(RedirectUri, mockHandler.Object, State: null),
            ticket
        );
        var httpContext = new DefaultHttpContext();
        mockHandler
            .Setup(x => x.ExecuteAsync(httpContext, result, ticket, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var executor = new DefaultAuthorizationResultExecutor();

        await executor.ExecuteAsync(httpContext, result, CancellationToken.None);
    }

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
            CreateLooseMock<OpenIdContext>().Object,
            CreateLooseMock<OpenIdClient>().Object,
            new AuthorizationResponseDelivery(
                RedirectUri,
                new QueryAuthorizationResponseModeHandler(),
                State: null
            ),
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
            RedirectUri,
            httpContext.Response.Headers.Location.ToString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseModeQuery_OmitsInternalParameters()
    {
        var result = CreateResult(new QueryAuthorizationResponseModeHandler());
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
        var result = CreateResult(new FormPostAuthorizationResponseModeHandler());
        var httpContext = new DefaultHttpContext();
        var body = new MemoryStream();
        httpContext.Response.Body = body;

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        body.Position = 0;
        var html = await new StreamReader(body, SecureEncoding.UTF8).ReadToEndAsync();

        Assert.Contains(NormalName, html, StringComparison.Ordinal);
        Assert.Contains(NormalValue, html, StringComparison.Ordinal);
        Assert.DoesNotContain("created_when", html, StringComparison.Ordinal);
        Assert.DoesNotContain(InternalValue, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseModeFormPost_AllowsOnlyTheFormsScriptAndItsClientsFrame()
    {
        var result = CreateResult(new FormPostAuthorizationResponseModeHandler());
        var httpContext = new DefaultHttpContext();
        var body = new MemoryStream();
        httpContext.Response.Body = body;

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        body.Position = 0;
        var html = await new StreamReader(body, SecureEncoding.UTF8).ReadToEndAsync();

        const string scriptStart = "<script>";
        var start = html.IndexOf(scriptStart, StringComparison.Ordinal) + scriptStart.Length;
        var end = html.IndexOf("</script>", start, StringComparison.Ordinal);
        var script = html[start..end];
        var hash = Convert.ToBase64String(SHA256.HashData(SecureEncoding.UTF8.GetBytes(script)));

        var headers = httpContext.Response.Headers;
        Assert.Equal(
            $"default-src 'none'; script-src 'sha256-{hash}'; base-uri 'none'; frame-ancestors https://client.example",
            headers.ContentSecurityPolicy.ToString()
        );
        Assert.False(headers.ContainsKey(HeaderNames.XFrameOptions));
        Assert.Equal("no-store, no-cache, max-age=0", headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("com.example.app:/callback")]
    [InlineData("https://[::1]:8443/callback")]
    public async Task ExecuteAsync_WhenTheFormPostRedirectUriHasNoHostOrigin_ForbidsFraming(
        string redirectUri
    )
    {
        var result = CreateResult(
            new FormPostAuthorizationResponseModeHandler(),
            redirectUri: redirectUri
        );
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        var headers = httpContext.Response.Headers;
        Assert.EndsWith("; frame-ancestors 'none'", headers.ContentSecurityPolicy.ToString());
        Assert.Equal("DENY", headers.XFrameOptions.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseModeFormPost_HtmlEncodesTheValuesWithoutUrlEncoding()
    {
        const string value = "a b=c/d+e'<f>";
        var result = CreateResult(
            new FormPostAuthorizationResponseModeHandler(),
            normalValue: value
        );

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
        const string redirectUri = "https://client.example/callback?x='y'";
        var result = CreateResult(
            new FormPostAuthorizationResponseModeHandler(),
            redirectUri: redirectUri
        );

        var html = await ExecuteFormPostAsync(Executor, result);

        Assert.Contains(
            $"action='{HtmlEncoder.Default.Encode(redirectUri)}'>",
            html,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("x='y'", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseModeQuery_RedirectsToTheRedirectUriAsWritten()
    {
        var result = CreateResult(
            new QueryAuthorizationResponseModeHandler(),
            redirectUri: "myapp://callback"
        );
        var httpContext = new DefaultHttpContext();

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        Assert.Equal(
            "myapp://callback?code=auth-code#_=_",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRegisteredQueryIsUnencoded_KeepsItAndReplacesACollidingName()
    {
        var result = CreateResult(
            new QueryAuthorizationResponseModeHandler(),
            redirectUri: "https://client.example/callback?path=a/b&code=stale"
        );
        var httpContext = new DefaultHttpContext();

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        Assert.Equal(
            "https://client.example/callback?path=a/b&code=auth-code#_=_",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseModeFragment_AppendsTheParametersToTheUriAsWritten()
    {
        var result = CreateResult(
            new FragmentAuthorizationResponseModeHandler(),
            redirectUri: "myapp://callback"
        );
        var httpContext = new DefaultHttpContext();

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        Assert.Equal(
            "myapp://callback#code=auth-code",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseModeFormPost_PostsToTheUriAsWritten()
    {
        var result = CreateResult(
            new FormPostAuthorizationResponseModeHandler(),
            redirectUri: "myapp://callback"
        );

        var html = await ExecuteFormPostAsync(Executor, result);

        Assert.Contains("action='myapp://callback'>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRedirectUriHasANonAsciiCharacter_PercentEncodesItInTheLocation()
    {
        var result = CreateResult(
            new QueryAuthorizationResponseModeHandler(),
            redirectUri: "https://client.example/caf\u00e9"
        );
        var httpContext = new DefaultHttpContext();

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        Assert.Equal(
            "https://client.example/caf%C3%A9?code=auth-code#_=_",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Theory]
    [InlineData(
        "https://client.example/cb\u0001x",
        "https://client.example/cb%01x?code=auth-code#_=_"
    )]
    [InlineData(
        "https://client.example/cb\u007fx",
        "https://client.example/cb%7Fx?code=auth-code#_=_"
    )]
    [InlineData(
        "https://client.example/cb\r\nSet-Cookie:a=b",
        "https://client.example/cb%0D%0ASet-Cookie:a=b?code=auth-code#_=_"
    )]
    public async Task ExecuteAsync_WhenTheRedirectUriHasAControlCharacter_PercentEncodesItInTheLocation(
        string redirectUri,
        string expected
    )
    {
        var result = CreateResult(
            new QueryAuthorizationResponseModeHandler(),
            redirectUri: redirectUri
        );
        var httpContext = new DefaultHttpContext();

        await Executor.ExecuteAsync(httpContext, result, CancellationToken.None);

        Assert.Equal(expected, httpContext.Response.Headers.Location.ToString());
    }

    #endregion
}
