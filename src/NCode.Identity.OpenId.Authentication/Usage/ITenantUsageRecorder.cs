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
/// Records a tenant's usage into the durable daily ledger (ADR-0199). Recording is in memory and never waits on the
/// database: each replica adds what it recorded to the ledger periodically.
/// </summary>
public interface ITenantUsageRecorder
{
    /// <summary>
    /// Records one unit of a tenant's usage of a meter on the current UTC calendar day.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <param name="meter">The name of the meter, such as <see cref="TenantUsageMeters.Requests"/>. Meters are an open
    /// vocabulary, so a package may record its own.</param>
    void Record(string tenantId, string meter);
}
