// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Polly;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public sealed class GuardedHttpClientTests
{
    #region IsRetryable Tests

    [Fact]
    public void IsRetryable_WhenTheResponseIs503_ReturnsTrue()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var outcome = Outcome.FromResult(response);

        var result = GuardedHttpClient.IsRetryable(outcome);

        Assert.True(result);
    }

    [Fact]
    public void IsRetryable_WhenTheConnectionWasRefused_ReturnsFalse()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(
            new HttpRequestException(HttpRequestError.ConnectionError, "refused")
        );

        var result = GuardedHttpClient.IsRetryable(outcome);

        Assert.False(result);
    }

    [Fact]
    public void IsRetryable_WhenTheNameDidNotResolve_ReturnsFalse()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(
            new HttpRequestException(HttpRequestError.NameResolutionError, "unresolved")
        );

        var result = GuardedHttpClient.IsRetryable(outcome);

        Assert.False(result);
    }

    [Fact]
    public void IsRetryable_WhenTheResponseIs404_ReturnsFalse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var outcome = Outcome.FromResult(response);

        var result = GuardedHttpClient.IsRetryable(outcome);

        Assert.False(result);
    }

    #endregion
}
