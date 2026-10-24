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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents the REST resource for a tenant's usage on a range of UTC calendar days, read from the durable daily
/// ledger the platform bills a plan by (ADR-0199).
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantUsageResource
{
    /// <summary>
    /// Gets the identifier of the tenant.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the first UTC calendar day of the range, inclusive.
    /// </summary>
    public required DateOnly From { get; init; }

    /// <summary>
    /// Gets the last UTC calendar day of the range, inclusive.
    /// </summary>
    public required DateOnly To { get; init; }

    /// <summary>
    /// Gets the usage of each meter on each day of the range that has any, ordered by day and then by meter. A day or
    /// meter without an entry had no usage.
    /// </summary>
    public required IReadOnlyList<TenantUsageEntry> Entries { get; init; }
}
