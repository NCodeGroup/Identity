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

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Contains the names of the tenant-usage meters the family records (ADR-0199). Meters are an open vocabulary: a
/// package records its own by name through <see cref="ITenantUsageRecorder"/>.
/// </summary>
public static class TenantUsageMeters
{
    /// <summary>
    /// Contains the name of the meter that counts the requests a tenant's protocol endpoints served: every request
    /// the rate limits and the daily request quota admitted.
    /// </summary>
    public const string Requests = "requests";

    /// <summary>
    /// Contains the name of the meter that counts the token requests of a tenant's confidential clients that the daily
    /// token-request quota admitted.
    /// </summary>
    public const string TokenRequests = "token_requests";
}
