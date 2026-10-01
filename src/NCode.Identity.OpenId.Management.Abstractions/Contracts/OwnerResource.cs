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

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Represents an owner of a resource: a role assignment that grants a principal authority over the resource and
/// everything beneath it.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class OwnerResource
{
    /// <summary>
    /// Gets the opaque identifier of the owner (role) assignment.
    /// </summary>
    public required string AssignmentId { get; init; }

    /// <summary>
    /// Gets the identifier of the principal that owns the resource.
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the name of the granted role.
    /// </summary>
    public required string RoleName { get; init; }
}
