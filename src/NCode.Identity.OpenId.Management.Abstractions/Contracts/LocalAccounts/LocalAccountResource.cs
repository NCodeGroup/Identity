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
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.LocalAccounts;

/// <summary>
/// Represents the REST resource for a local account: the server-owned credential, profile, and status payload behind a
/// self-issued connection. The credential (password hash) and security stamp are never exposed.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class LocalAccountResource : ISupportTenantId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the opaque identifier of the local account (its self-issued subject).
    /// </summary>
    public required string LocalAccountId { get; init; }

    /// <summary>
    /// Gets the account's username (login handle), unique within the tenant.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the account's email address, or <c>null</c> when none is recorded.
    /// </summary>
    public required string? Email { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account's <see cref="Email"/> is verified.
    /// </summary>
    public required bool EmailVerified { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account is enabled.
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }
}
