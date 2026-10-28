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

using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Auditing;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public sealed class AuditRequestFactoryTests
{
    private const string EndpointName = "token";

    #region Create Tests

    [Fact]
    public void Create_WhenTheRequestIsPopulated_CapturesTheRequestId()
    {
        var httpContext = CreatePopulatedHttpContext();

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Equal("req-1", request.RequestId);
    }

    [Fact]
    public void Create_WhenAW3CActivityIsCurrent_CapturesTheTraceAndSpanIds()
    {
        var httpContext = CreatePopulatedHttpContext();
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Equal(activity.TraceId.ToHexString(), request.TraceId);
        Assert.Equal(activity.SpanId.ToHexString(), request.SpanId);
    }

    [Fact]
    public void Create_WhenAHierarchicalActivityIsCurrent_CapturesNoTraceOrSpanId()
    {
        var httpContext = CreatePopulatedHttpContext();
        using var activity = new Activity("test")
            .SetIdFormat(ActivityIdFormat.Hierarchical)
            .Start();

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Null(request.TraceId);
        Assert.Null(request.SpanId);
    }

    [Fact]
    public void Create_WhenNoActivityIsCurrent_CapturesNoTraceOrSpanId()
    {
        var httpContext = CreatePopulatedHttpContext();
        Activity.Current = null;

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Null(request.TraceId);
        Assert.Null(request.SpanId);
    }

    [Fact]
    public void Create_WhenTheRequestIsPopulated_CapturesTheSourceIp()
    {
        var httpContext = CreatePopulatedHttpContext();

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Equal("203.0.113.5", request.SourceIp);
    }

    [Fact]
    public void Create_WhenTheRequestIsPopulated_CapturesTheUserAgent()
    {
        var httpContext = CreatePopulatedHttpContext();

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Equal("curl/8.0", request.UserAgent);
    }

    [Fact]
    public void Create_WhenTheRequestIsPopulated_CapturesTheHttpMethod()
    {
        var httpContext = CreatePopulatedHttpContext();

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Equal("POST", request.HttpMethod);
    }

    [Fact]
    public void Create_WhenTheRequestIsPopulated_CapturesThePathBaseAndPathWithoutTheQueryString()
    {
        var httpContext = CreatePopulatedHttpContext();

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Equal("/id/oauth2/token", request.Path);
    }

    [Fact]
    public void Create_WhenAnEndpointIsGiven_CapturesTheEndpoint()
    {
        var httpContext = CreatePopulatedHttpContext();

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Equal(EndpointName, request.Endpoint);
    }

    [Fact]
    public void Create_WhenTheUserAgentIsLongerThanTheLimit_TruncatesIt()
    {
        var httpContext = CreatePopulatedHttpContext();
        httpContext.Request.Headers.UserAgent = new string('a', 600);

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Equal(new string('a', AuditRequestFactory.MaxUserAgentLength), request.UserAgent);
    }

    [Fact]
    public void Create_WhenTheUserAgentIsExactlyTheLimit_KeepsIt()
    {
        var httpContext = CreatePopulatedHttpContext();
        httpContext.Request.Headers.UserAgent = new string(
            'a',
            AuditRequestFactory.MaxUserAgentLength
        );

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Equal(new string('a', AuditRequestFactory.MaxUserAgentLength), request.UserAgent);
    }

    [Fact]
    public void Create_WhenTheLimitSplitsASurrogatePair_DropsTheFirstHalf()
    {
        var httpContext = CreatePopulatedHttpContext();
        httpContext.Request.Headers.UserAgent =
            new string('a', AuditRequestFactory.MaxUserAgentLength - 1) + "\U0001F600" + "tail";

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Equal(
            new string('a', AuditRequestFactory.MaxUserAgentLength - 1),
            request.UserAgent
        );
    }

    [Fact]
    public void Create_WhenTheRequestIsEmpty_CapturesNothingButTheEndpoint()
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = string.Empty };
        Activity.Current = null;

        var request = AuditRequestFactory.Create(httpContext, EndpointName);

        Assert.Null(request.RequestId);
        Assert.Null(request.TraceId);
        Assert.Null(request.SpanId);
        Assert.Null(request.SourceIp);
        Assert.Null(request.UserAgent);
        Assert.Null(request.HttpMethod);
        Assert.Null(request.Path);
        Assert.Equal(EndpointName, request.Endpoint);
    }

    [Fact]
    public void Create_WhenNoEndpointIsGiven_CapturesNoEndpoint()
    {
        var httpContext = CreatePopulatedHttpContext();

        var request = AuditRequestFactory.Create(httpContext, endpoint: null);

        Assert.Null(request.Endpoint);
    }

    #endregion

    private static DefaultHttpContext CreatePopulatedHttpContext()
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = "req-1" };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.PathBase = "/id";
        httpContext.Request.Path = "/oauth2/token";
        httpContext.Request.QueryString = new QueryString("?code=secret");
        httpContext.Request.Headers.UserAgent = "curl/8.0";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");
        return httpContext;
    }
}
