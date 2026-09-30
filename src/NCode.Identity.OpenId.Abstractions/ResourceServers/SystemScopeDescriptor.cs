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

namespace NCode.Identity.OpenId.ResourceServers;

/// <summary>
/// Describes a single scope on a system resource server that is seeded into a tenant.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SystemScopeDescriptor
{
    /// <summary>
    /// Gets the value of the scope (for example, <c>openid</c> or <c>read:clients</c>).
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the human-readable description of the scope, or <c>null</c> when none is provided.
    /// </summary>
    public required string? Description { get; init; }
}
