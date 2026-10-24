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
/// The outcome of judging a request against the rate limits of a stage: admitted, or refused until
/// <see cref="RetryAfter"/> has elapsed. The default value admits the request.
/// </summary>
/// <param name="RetryAfter">How long the caller waits before retrying; <see langword="null"/> when admitted.</param>
// See ADR-0196 and ADR-0246.
[PublicAPI]
public readonly record struct RateLimitDecision(TimeSpan? RetryAfter)
{
    /// <summary>
    /// Gets the decision that admits a request.
    /// </summary>
    public static RateLimitDecision Admitted => default;

    /// <summary>
    /// Gets a value indicating whether the request is refused.
    /// </summary>
    public bool IsRejected => RetryAfter.HasValue;
}
