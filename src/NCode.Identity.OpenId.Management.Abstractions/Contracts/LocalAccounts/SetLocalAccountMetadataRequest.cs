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

namespace NCode.Identity.OpenId.Management.Contracts.LocalAccounts;

/// <summary>
/// Represents the request body to set a local account's server-owned metadata bags. Each bag is replaced only when
/// present in the request; an omitted bag is left unchanged. Because the management API is administrator-gated, this is
/// the server/admin write path through which the server-controlled <c>SystemMetadata</c> may be set — it is never
/// end-user writable (ADR-0054, ADR-0055).
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SetLocalAccountMetadataRequest
{
    /// <summary>
    /// Gets the replacement owner-updatable profile metadata as a free-form JSON object, or <c>null</c> to leave the
    /// current value unchanged.
    /// </summary>
    public JsonElement? ProfileMetadata { get; init; }

    /// <summary>
    /// Gets the replacement server-controlled system metadata as a free-form JSON object, or <c>null</c> to leave the
    /// current value unchanged.
    /// </summary>
    public JsonElement? SystemMetadata { get; init; }
}
