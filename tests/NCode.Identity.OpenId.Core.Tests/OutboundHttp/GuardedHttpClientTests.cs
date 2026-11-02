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
