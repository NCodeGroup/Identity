// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.Management.Contracts;

/// <summary>
/// Represents the mutable profile fields of a local account that a JSON Patch document may modify. The credential,
/// enabled status, and metadata are managed through their own endpoints.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateLocalAccountRequest
{
    /// <summary>
    /// Gets or sets the account's username (login handle), unique within the tenant.
    /// </summary>
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account's email address, or <c>null</c> when none is recorded.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account's <see cref="Email"/> is verified.
    /// </summary>
    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; set; }
}
