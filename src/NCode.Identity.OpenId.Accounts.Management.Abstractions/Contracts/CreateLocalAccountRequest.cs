// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.Management.Contracts;

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
    /// or <c>null</c> for an empty object.
    /// </summary>
    public JsonElement? ProfileMetadata { get; init; }

    /// <summary>
    /// Gets the server-controlled system metadata to set on the account's owning principal as a free-form JSON object,
    /// or <c>null</c> for an empty object. It may carry authorization-relevant data and is never end-user writable; the
    /// administrator-gated management API is its only write path.
    /// </summary>
    public JsonElement? SystemMetadata { get; init; }
}
