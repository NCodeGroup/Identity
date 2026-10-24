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
using NCode.Identity.OpenId.Accounts.DataContracts;

namespace NCode.Identity.OpenId.Accounts.Stores;

/// <summary>
/// Contains the outcome of one verification of a local account's credential through
/// <see cref="ILocalAccountStore"/>.
/// </summary>
[PublicAPI]
public sealed class LocalAccountVerificationResult
{
    /// <summary>
    /// Gets the user name that the account the store found holds, or the submitted user name when no account has it.
    /// The limit on failed password attempts counts it.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the account whose password verified, whether or not it is enabled, or <c>null</c>.
    /// </summary>
    public PersistedLocalAccount? Account { get; init; }

    /// <summary>
    /// Gets a value indicating whether the store's caller refused the password, so the store verified it against its
    /// dummy hash and returned no account.
    /// </summary>
    public bool IsRefused { get; init; }

    /// <summary>
    /// Gets a value indicating whether the password verified and <see cref="Account"/> is not <c>null</c>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Account))]
    public bool IsVerified => Account is not null;
}
