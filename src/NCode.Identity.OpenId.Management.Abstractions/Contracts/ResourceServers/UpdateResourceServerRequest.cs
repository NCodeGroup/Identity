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
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the mutable metadata of a resource server that a JSON Patch document may modify.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateResourceServerRequest
{
    /// <summary>
    /// Gets or sets the human-readable name for the resource server.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the resource server is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the JSON settings for the resource server.
    /// </summary>
    public required JsonElement Settings { get; set; }
}
