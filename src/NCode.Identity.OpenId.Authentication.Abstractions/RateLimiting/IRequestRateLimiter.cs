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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Judges a request against the rate limits and quotas registered for a named stage. The server judges the stages
/// named by <see cref="RateLimitStages"/>; a package that registers <see cref="RateLimitDescriptor"/> instances for a
/// stage of its own judges that stage by calling <see cref="AcquireAsync"/> with the stage's name. Each call counts the
/// request in every limit it judges, so a refused request still counts.
/// </summary>
// See ADR-0196 and ADR-0246.
[PublicAPI]
public interface IRequestRateLimiter
{
    /// <summary>
    /// Judges every limit registered for a stage in one counter-store call, in the order of their descriptors, and
    /// stops at the first that refuses the request, so a request one limit refuses is not counted against the next.
    /// The limiter marks a refused request on the request's rate-limit state, so the request is not billed as the
    /// tenant's usage. An admitted request has the usage meters of the stage added to that state, for the endpoint
    /// filter to record once the endpoint has run.
    /// </summary>
    /// <param name="stage">The name of the stage to judge, such as one of the <see cref="RateLimitStages"/>.</param>
    /// <param name="context">The <see cref="RateLimitContext"/> of the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="RateLimitDecision"/> for the request: admitted, or refused until its retry time. A
    /// request admitted past a quota as billable overage is admitted, and the overage usage meter records it.</returns>
    ValueTask<RateLimitDecision> AcquireAsync(
        string stage,
        RateLimitContext context,
        CancellationToken cancellationToken
    );
}
