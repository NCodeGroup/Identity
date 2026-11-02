// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
            .ErrorFactory.Create(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable)
            .WithDescription("The request rate limit has been exceeded.")
            .WithStatusCode(StatusCodes.Status429TooManyRequests)
            .WithResponseHeader(
                HeaderNames.RetryAfter,
                seconds.ToString(CultureInfo.InvariantCulture)
            )
            .WithResponseHeader(HeaderNames.CacheControl, "no-store");
    }
}
