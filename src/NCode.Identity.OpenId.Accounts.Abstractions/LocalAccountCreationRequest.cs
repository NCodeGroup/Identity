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

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Json;

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// Describes a request to create a local account: the login handle, the initial credential (carried as caller-owned
/// bytes so it never lingers as a managed <see cref="string"/>), and optional profile/status fields.
/// </summary>
[PublicAPI]
public sealed class LocalAccountCreationRequest
{
    /// <summary>
    /// Gets the account's username (login handle), unique within the tenant.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the UTF-8 bytes of the account's initial password, carried in a caller-owned buffer so the sensitive
    /// material can be zeroed after use and never lingers as a managed <see cref="string"/>.
    /// </summary>
    public required ReadOnlyMemory<byte> Password { get; init; }

    /// <summary>
    /// Gets the account's email address, or <c>null</c> when none is recorded.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account's <see cref="Email"/> is already verified.
    /// </summary>
    public bool EmailVerified { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account is enabled. Defaults to <c>true</c>.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets the owner-updatable profile metadata as a free-form JSON object, defaulting to an empty object <c>{}</c>.
    /// Any key/value shape is permitted; this bag is intended to be read, and updated, by the account owner.
    /// </summary>
    public JsonElement ProfileMetadata { get; init; } = JsonElements.EmptyObject;

    /// <summary>
    /// Gets the server-controlled system metadata as a free-form JSON object, defaulting to an empty object <c>{}</c>.
    /// This bag is managed only by the server or an administrator and must never be writable by the end user, as it
    /// may carry authorization-relevant data.
    /// </summary>
    public JsonElement SystemMetadata { get; init; } = JsonElements.EmptyObject;
}
