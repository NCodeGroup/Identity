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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// Describes a request to update the mutable profile fields of an existing local account. The credential and enabled
/// status are managed through <see cref="ILocalAccountProvisioner.ResetPasswordAsync"/> and
/// <see cref="ILocalAccountProvisioner.SetEnabledAsync"/> respectively.
/// </summary>
[PublicAPI]
public sealed class LocalAccountUpdateRequest
{
    /// <summary>
    /// Gets the identifier of the local account to update (its self-issued subject).
    /// </summary>
    public required string LocalAccountId { get; init; }

    /// <summary>
    /// Gets the account's username (login handle), unique within the tenant.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the account's email address, or <c>null</c> when none is recorded.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account's <see cref="Email"/> is verified.
    /// </summary>
    public bool EmailVerified { get; init; }
}
