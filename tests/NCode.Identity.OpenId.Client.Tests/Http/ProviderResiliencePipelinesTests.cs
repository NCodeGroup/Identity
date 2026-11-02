// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Options;
using Polly;
using Polly.Timeout;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Http;

public class ProviderResiliencePipelinesTests : BaseTests
{
    private TestMetrics Metrics { get; } = new();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Metrics.Dispose();
        base.Dispose(disposing);
    }

    private ProviderResiliencePipelines CreatePipelines()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OpenIdClientOptions());
        return new ProviderResiliencePipelines(options, Metrics.CreateProviderMetrics());
    }

    #region IsTransient Tests

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public void IsTransient_WhenTheResponseIsTransient_ReturnsTrue(HttpStatusCode statusCode)
    {
        using var response = new HttpResponseMessage(statusCode);
        var outcome = Outcome.FromResult(response);

        var result = ProviderResiliencePipelines.IsTransient(outcome);

        Assert.True(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.OK)]
    public void IsTransient_WhenTheResponseIsNotTransient_ReturnsFalse(HttpStatusCode statusCode)
    {
        using var response = new HttpResponseMessage(statusCode);
        var outcome = Outcome.FromResult(response);

        var result = ProviderResiliencePipelines.IsTransient(outcome);

        Assert.False(result);
    }

    [Fact]
    public void IsTransient_WhenTheConnectionWasRefused_ReturnsTrue()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(
            new HttpRequestException(HttpRequestError.ConnectionError, "refused")
        );

        var result = ProviderResiliencePipelines.IsTransient(outcome);

        Assert.True(result);
    }

    [Fact]
    public void IsTransient_WhenTheAttemptTimedOut_ReturnsTrue()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(
            new TimeoutRejectedException("timed out", TimeSpan.FromSeconds(1))
        );

        var result = ProviderResiliencePipelines.IsTransient(outcome);

        Assert.True(result);
    }

    #endregion

    #region IsRetryable Tests

    [Fact]
    public void IsRetryable_WhenTheRequestIsAGet_ReturnsTrue()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://op.example/jwks");

        var result = ProviderResiliencePipelines.IsRetryable(request);

        Assert.True(result);
    }

    [Fact]
    public void IsRetryable_WhenTheRequestIsAHead_ReturnsTrue()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, "https://op.example/jwks");

        var result = ProviderResiliencePipelines.IsRetryable(request);

        Assert.True(result);
    }

    [Fact]
    public void IsRetryable_WhenTheRequestIsAPost_ReturnsFalse()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://op.example/token");

        var result = ProviderResiliencePipelines.IsRetryable(request);

        Assert.False(result);
    }

    [Fact]
    public void IsRetryable_WhenAGetCarriesADPoPProof_ReturnsFalse()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://op.example/userinfo");
        request.Headers.Add(OpenIdProtocol.DPoP.HeaderName, "proof");

        var result = ProviderResiliencePipelines.IsRetryable(request);

        Assert.False(result);
    }

    [Fact]
    public void IsRetryable_WhenAGetCarriesABody_ReturnsFalse()
    {
        using var content = new ByteArrayContent([0x7B, 0x7D]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://op.example/userinfo");
        request.Content = content;

        var result = ProviderResiliencePipelines.IsRetryable(request);

        Assert.False(result);
    }

    #endregion

    #region Select Tests

    [Fact]
    public void Select_WhenTheRequestsAreAGetAndAHead_ReturnsTheSamePipeline()
    {
        var pipelines = CreatePipelines();
        using var get = new HttpRequestMessage(HttpMethod.Get, "https://op.example/jwks");
        using var head = new HttpRequestMessage(HttpMethod.Head, "https://op.example/jwks");

        var forGet = pipelines.Select(get);
        var forHead = pipelines.Select(head);

        Assert.Same(forGet, forHead);
    }

    [Fact]
    public void Select_WhenTheRequestIsAPost_ReturnsADifferentPipeline()
    {
        var pipelines = CreatePipelines();
        using var get = new HttpRequestMessage(HttpMethod.Get, "https://op.example/jwks");
        using var post = new HttpRequestMessage(HttpMethod.Post, "https://op.example/token");

        var forGet = pipelines.Select(get);
        var forPost = pipelines.Select(post);

        Assert.NotSame(forGet, forPost);
    }

    #endregion

    #region GetErrorType Tests

    [Fact]
    public void GetErrorType_WhenTheOutcomeIsAResponse_ReturnsItsStatusCode()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        var errorType = ProviderResiliencePipelines.GetErrorType(Outcome.FromResult(response));

        Assert.Equal("503", errorType);
    }

    [Fact]
    public void GetErrorType_WhenTheAttemptTimedOut_ReturnsTimeout()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(new TimeoutRejectedException());

        var errorType = ProviderResiliencePipelines.GetErrorType(outcome);

        Assert.Equal("timeout", errorType);
    }

    [Theory]
    [InlineData(HttpRequestError.ConnectionError, "connection_error")]
    [InlineData(HttpRequestError.NameResolutionError, "name_resolution_error")]
    [InlineData(HttpRequestError.Unknown, "unknown")]
    public void GetErrorType_WhenTheRequestFailed_ReturnsTheRequestErrorInSnakeCase(
        HttpRequestError requestError,
        string expected
    )
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(
            new HttpRequestException(requestError)
        );

        var errorType = ProviderResiliencePipelines.GetErrorType(outcome);

        Assert.Equal(expected, errorType);
    }

    [Fact]
    public void GetErrorType_WhenTheExceptionIsUnexpected_ReturnsOther()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(new InvalidOperationException());

        var errorType = ProviderResiliencePipelines.GetErrorType(outcome);

        Assert.Equal("_OTHER", errorType);
    }

    #endregion
}
