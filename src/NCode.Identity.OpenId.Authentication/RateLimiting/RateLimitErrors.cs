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

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Builds the error a request refused by a rate limit is answered with: <c>429 Too Many Requests</c> (RFC 6585 §4)
/// with the <c>temporarily_unavailable</c> error code, a <c>Retry-After</c> in whole seconds (RFC 9110 §10.2.3), and
/// <c>Cache-Control: no-store</c>.
/// </summary>
internal static class RateLimitErrors
{
    /// <summary>
    /// Creates the error for a refused request. The request rate limiter marks a request it refuses, so this does not.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> of the request.</param>
    /// <param name="retryAfter">How long the caller waits before retrying.</param>
    /// <returns>The <see cref="IOpenIdError"/> to answer the request with.</returns>
    public static IOpenIdError Refuse(OpenIdContext openIdContext, TimeSpan retryAfter)
    {
        // Retry-After counts whole seconds, so round up and never tell the caller to retry at once.
        var seconds = Math.Max(1L, (long)Math.Ceiling(retryAfter.TotalSeconds));

        return openIdContext
            .ErrorFactory.Create(OpenIdConstants.ErrorCodes.TemporarilyUnavailable)
            .WithDescription("The request rate limit has been exceeded.")
            .WithStatusCode(StatusCodes.Status429TooManyRequests)
            .WithResponseHeader(
                HeaderNames.RetryAfter,
                seconds.ToString(CultureInfo.InvariantCulture)
            )
            .WithResponseHeader(HeaderNames.CacheControl, "no-store");
    }
}
