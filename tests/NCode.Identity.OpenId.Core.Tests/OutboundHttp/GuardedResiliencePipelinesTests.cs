// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Polly;
using Polly.Timeout;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public sealed class GuardedResiliencePipelinesTests
{
    #region IsTransient Tests

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public void IsTransient_WhenTheResponseIsTransient_ReturnsTrue(HttpStatusCode statusCode)
    {
        using var response = new HttpResponseMessage(statusCode);
        var outcome = Outcome.FromResult(response);

        var result = GuardedResiliencePipelines.IsTransient(outcome);

        Assert.True(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.OK)]
    public void IsTransient_WhenTheResponseIsNotTransient_ReturnsFalse(HttpStatusCode statusCode)
    {
        using var response = new HttpResponseMessage(statusCode);
        var outcome = Outcome.FromResult(response);

        var result = GuardedResiliencePipelines.IsTransient(outcome);

        Assert.False(result);
    }

    [Fact]
    public void IsTransient_WhenTheConnectionWasRefused_ReturnsFalse()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(
            new HttpRequestException(HttpRequestError.ConnectionError, "refused")
        );

        var result = GuardedResiliencePipelines.IsTransient(outcome);

        Assert.False(result);
    }

    [Fact]
    public void IsTransient_WhenTheNameDidNotResolve_ReturnsFalse()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(
            new HttpRequestException(HttpRequestError.NameResolutionError, "unresolved")
        );

        var result = GuardedResiliencePipelines.IsTransient(outcome);

        Assert.False(result);
    }

    [Fact]
    public void IsTransient_WhenTheResponseEnded_ReturnsTrue()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(
            new HttpRequestException(HttpRequestError.ResponseEnded, "ended")
        );

        var result = GuardedResiliencePipelines.IsTransient(outcome);

        Assert.True(result);
    }

    [Fact]
    public void IsTransient_WhenAnAttemptTimedOut_ReturnsTrue()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(
            new TimeoutRejectedException("timed out", TimeSpan.FromMilliseconds(50))
        );

        var result = GuardedResiliencePipelines.IsTransient(outcome);

        Assert.True(result);
    }

    #endregion

    #region IsRetryable Tests

    [Fact]
    public void IsRetryable_WhenTheRequestIsAGet_ReturnsTrue()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");

        var result = GuardedResiliencePipelines.IsRetryable(request);

        Assert.True(result);
    }

    [Fact]
    public void IsRetryable_WhenTheRequestIsAHead_ReturnsTrue()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, "https://client.example/jwks");

        var result = GuardedResiliencePipelines.IsRetryable(request);

        Assert.True(result);
    }

    [Fact]
    public void IsRetryable_WhenTheRequestIsAPost_ReturnsFalse()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://client.example/notify"
        );

        var result = GuardedResiliencePipelines.IsRetryable(request);

        Assert.False(result);
    }

    [Fact]
    public void IsRetryable_WhenAGetHasABody_ReturnsFalse()
    {
        using var content = new ByteArrayContent([0x7B, 0x7D]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://client.example/jwks");
        request.Content = content;

        var result = GuardedResiliencePipelines.IsRetryable(request);

        Assert.False(result);
    }

    #endregion
}
