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
using NCode.Identity.OpenId.Authentication.RateLimiting;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.RateLimiting;

public sealed class RateLimitRequestStateTests
{
    #region Get Tests

    [Fact]
    public void Get_WhenNothingIsStored_StoresAnEmptyState()
    {
        var httpContext = new DefaultHttpContext();

        var first = RateLimitRequestState.Get(httpContext);
        var second = RateLimitRequestState.Get(httpContext);

        Assert.False(first.IsRefused);
        Assert.Empty(first.UsageMeters);
        Assert.Same(first, second);
    }

    [Fact]
    public void Get_ForAnotherRequest_ReturnsItsOwnState()
    {
        var firstRequest = new DefaultHttpContext();
        var secondRequest = new DefaultHttpContext();
        var firstState = RateLimitRequestState.Get(firstRequest);
        firstState.IsRefused = true;
        firstState.UsageMeters.Add("requests");

        var actual = RateLimitRequestState.Get(secondRequest);

        Assert.False(actual.IsRefused);
        Assert.Empty(actual.UsageMeters);
        Assert.NotSame(firstState, actual);
    }

    #endregion
}
