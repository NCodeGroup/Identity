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

using System.Diagnostics.Metrics;

namespace NCode.Identity.OpenId.Authentication.Metrics;

/// <summary>
/// Owns the instruments of the <see cref="OpenIdAuthenticationMetrics.MeterName"/> meter, created once through the
/// <see cref="IMeterFactory"/> so each service provider has its own; call sites record through its methods and never
/// touch the meter directly.
/// </summary>
// See ADR-0197.
internal sealed class AuthenticationMetrics
{
    internal const string RateLimitingRequestsName = "ncode.identity.rate_limiting.requests";

    internal const string RateLimitingPartitionAttribute = "ncode.identity.rate_limiting.partition";

    internal const string RateLimitingResultAttribute = "ncode.identity.rate_limiting.result";

    internal const string AdmittedResult = "admitted";

    internal const string RejectedResult = "rejected";

    internal const string StoreUnavailableResult = "store_unavailable";

    private Counter<long> RateLimitingRequests { get; }

    public AuthenticationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(OpenIdAuthenticationMetrics.MeterName);

        RateLimitingRequests = meter.CreateCounter<long>(
            RateLimitingRequestsName,
            unit: "{request}",
            description: "The number of requests judged against a rate-limit partition, by partition and result."
        );
    }

    /// <summary>
    /// Records that a request was judged against a rate-limit partition.
    /// </summary>
    /// <param name="partition">The bounded partition name: <c>tenant</c>, <c>address</c>, or <c>client</c>.</param>
    /// <param name="result">The bounded result: <c>admitted</c>, <c>rejected</c>, or <c>store_unavailable</c>.</param>
    public void RateLimitJudged(string partition, string result) =>
        RateLimitingRequests.Add(
            1,
            new KeyValuePair<string, object?>(RateLimitingPartitionAttribute, partition),
            new KeyValuePair<string, object?>(RateLimitingResultAttribute, result)
        );
}
