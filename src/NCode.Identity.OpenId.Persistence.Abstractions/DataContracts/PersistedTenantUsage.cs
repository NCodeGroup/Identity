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

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains a tenant's usage of one meter on one UTC calendar day: the durable count the platform bills a plan by and
/// reports to the customer (ADR-0199).
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedTenantUsage : ISupportTenantId
{
    /// <summary>
    /// Gets the identifier of the tenant whose usage this is.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the UTC calendar day the usage was counted on.
    /// </summary>
    public required DateOnly Day { get; init; }

    /// <summary>
    /// Gets the name of the meter, such as <c>requests</c> or <c>token_requests</c>. Meters are an open vocabulary.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.UsageMeter)]
    public required string Meter { get; init; }

    /// <summary>
    /// Gets the count of the meter on the day.
    /// </summary>
    public required long Count { get; init; }
}
