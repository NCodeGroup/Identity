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

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// A <see cref="RateLimitDescriptor"/> that applies to a request, with the partition value the request counts in and
/// the limit and overage that the request's settings give it.
/// </summary>
/// <param name="Descriptor">The descriptor that applies to the request.</param>
/// <param name="PartitionValue">The partition of the limit that the request counts in.</param>
/// <param name="Limit">The most requests the limit admits in one window.</param>
/// <param name="Overage">The most requests admitted past <paramref name="Limit"/> as overage; zero when there is none.</param>
// See ADR-0246.
internal readonly record struct JudgedRateLimit(
    RateLimitDescriptor Descriptor,
    string PartitionValue,
    int Limit,
    int Overage
);
