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
/// Represents a tenant's effective environment within a <see cref="TenantResource"/>.
/// </summary>
// See ADR-0289.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantEnvironmentResource
{
    /// <summary>
    /// Gets the effective <c>environment_type</c>, such as <c>production</c> or <c>development</c>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the effective <c>environment_name</c>, a display label, or <see langword="null"/> when there is none.
    /// </summary>
    public required string? Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the registered type is production-grade
    /// (its <c>ITenantEnvironmentType.IsProduction</c>). The vocabulary of types is open, so a client tells
    /// production from every other environment by this value rather than by the type's name.
    /// </summary>
    public required bool IsProduction { get; init; }
}
