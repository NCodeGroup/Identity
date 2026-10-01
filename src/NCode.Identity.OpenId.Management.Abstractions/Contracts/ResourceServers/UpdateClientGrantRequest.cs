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

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the request body to replace the granted scopes of a client grant.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateClientGrantRequest
{
    /// <summary>
    /// Gets the granted scope values. Each must be a scope defined on the target resource server.
    /// </summary>
    public required IReadOnlyList<string> Scopes { get; init; }
}
