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

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Judges a request to a tenant's protocol endpoints against the tenant's rate limits
/// (<c>rate_limit_tenant_per_minute</c>, <c>rate_limit_address_per_minute</c>, and
/// <c>rate_limit_client_per_minute</c>) and daily quotas (<c>quota_tenant_requests_per_day</c> and
/// <c>quota_tenant_token_requests_per_day</c>). Each call counts the request, so a refused request still counts.
/// </summary>
internal interface IRequestRateLimiter
{
    /// <summary>
    /// Counts the request against the limit for its remote address, the tenant-wide limit, and, when the request is
    /// metered, the tenant's daily request quota, in that order; a request one of them refuses is not counted against
    /// the next.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> of the request.</param>
    /// <param name="isMetered">Whether the request is the tenant's metered usage and so spends its daily request quota.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="RateLimitDecision"/> for the request.</returns>
    ValueTask<RateLimitDecision> AcquireForTenantAsync(
        OpenIdContext openIdContext,
        bool isMetered,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Counts the request against the limit for the client it authenticated as.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> of the request.</param>
    /// <param name="client">The authenticated <see cref="OpenIdClient"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="RateLimitDecision"/> for the request.</returns>
    ValueTask<RateLimitDecision> AcquireForClientAsync(
        OpenIdContext openIdContext,
        OpenIdClient client,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Counts a token request from an authenticated confidential client against the tenant's daily token-request quota.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> of the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="RateLimitDecision"/> for the request.</returns>
    ValueTask<RateLimitDecision> AcquireForTokenRequestAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    );
}
