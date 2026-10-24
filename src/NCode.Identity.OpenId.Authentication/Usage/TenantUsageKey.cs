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
/// Identifies a tenant's usage of one meter on one UTC calendar day.
/// </summary>
/// <param name="TenantId">The identifier of the tenant.</param>
/// <param name="Day">The UTC calendar day.</param>
/// <param name="Meter">The name of the meter.</param>
internal readonly record struct TenantUsageKey(string TenantId, DateOnly Day, string Meter);
