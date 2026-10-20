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

namespace NCode.Identity.OpenId.Management.Contracts.Clients;

/// <summary>
/// Represents the request body to create a new OpenID Client.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateClientRequest
{
    /// <summary>
    /// Gets a value indicating whether the client is created in a disabled state.
    /// </summary>
    public required bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the client acts for, or <c>null</c> for an ordinary client. A client
    /// that acts for a resource server is that resource server's credential at the token introspection endpoint — where
    /// it learns about the access tokens issued for the resource server — and is accepted at no other endpoint. The
    /// resource server must belong to the tenant and must not be a system resource server, and the caller must be
    /// allowed to update it. The binding cannot be changed after the client is created.
    /// </summary>
    public string? ResourceServerId { get; init; }

    /// <summary>
    /// Gets the initial JSON settings for the client.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
