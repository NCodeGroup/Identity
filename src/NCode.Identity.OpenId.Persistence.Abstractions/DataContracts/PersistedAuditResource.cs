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
/// Contains an entity an audited operation acted on, as an audit store keeps it.
/// </summary>
// See ADR-0303.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedAuditResource
{
    /// <summary>
    /// Gets the type of the resource, such as <c>client</c>. Resource types are an open vocabulary.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the identifier of the resource, or <see langword="null"/> when it has none or it is longer than the store
    /// indexes.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public string? Id { get; init; }
}
