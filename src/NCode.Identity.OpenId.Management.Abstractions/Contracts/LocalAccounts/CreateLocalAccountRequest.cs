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
/// Represents the request body to create a new local account. The password is supplied as plaintext over the
/// (TLS-protected) wire and is hashed server-side; it is never persisted or returned.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateLocalAccountRequest
{
    /// <summary>
    /// Gets the account's username (login handle), unique within the tenant.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the account's initial password, hashed server-side on create.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// Gets the account's email address, or <c>null</c> when none is recorded.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account's <see cref="Email"/> is already verified.
    /// </summary>
    public bool EmailVerified { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account is created in an enabled state. Defaults to <c>true</c>.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets the account's initial profile claims, or <c>null</c> when none are set at creation.
    /// </summary>
    public IReadOnlyList<LocalAccountClaim>? Claims { get; init; }

    /// <summary>
    /// Gets the owner-updatable profile metadata to set on the account's owning principal as a free-form JSON object,
    /// or <c>null</c> for an empty object (ADR-0055).
    /// </summary>
    public JsonElement? ProfileMetadata { get; init; }

    /// <summary>
    /// Gets the server-controlled system metadata to set on the account's owning principal as a free-form JSON object,
    /// or <c>null</c> for an empty object. It may carry authorization-relevant data and is never end-user writable; the
    /// administrator-gated management API is its only write path (ADR-0054, ADR-0055).
    /// </summary>
    public JsonElement? SystemMetadata { get; init; }
}
