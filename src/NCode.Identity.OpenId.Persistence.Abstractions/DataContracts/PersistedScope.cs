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

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted scope (a permission, in Auth0 terms) owned by a resource server.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedScope
{
    /// <summary>
    /// Gets or sets the value of this scope (for example, <c>read:messages</c>).
    /// </summary>
    [MaxLength(OpenIdMaxLengths.ScopeValue)]
    public required string Value { get; init; }

    /// <summary>
    /// Gets or sets the human-readable description for this scope.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.Description)]
    public required string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this scope is a reserved, system-owned scope that cannot be deleted.
    /// </summary>
    public required bool IsSystem { get; init; }
}
