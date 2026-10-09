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

using System.Security.Claims;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// Describes a local account resolved by an <see cref="ILocalAccountSource"/>: the server-owned credential, profile,
/// and status payload behind a self-issued connection. This is the provider-neutral runtime view that both the
/// credential-validation and claims/status paths consume; it is distinct from the persisted
/// <c>PersistedLocalAccount</c> storage shape, so a source backed by an external store (such as ASP.NET Core Identity)
/// need not expose the persistence contract.
/// </summary>
[PublicAPI]
public sealed class LocalAccount
{
    /// <summary>
    /// Gets the account's stable subject value: the <c>subject</c> of its self-issued connection identity, from which
    /// the owning principal (and therefore the emitted <c>sub</c> claim) is resolved.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account is enabled. A disabled account must not be allowed to authenticate
    /// or to satisfy an active-status check.
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// Gets the account's security stamp, a value that changes whenever credentials change so that outstanding
    /// sessions and tokens can be invalidated. This value is <c>null</c> when the source does not track one.
    /// </summary>
    public string? SecurityStamp { get; init; }

    /// <summary>
    /// Gets the account's profile claims, contributed to the issued tokens and the UserInfo response by the claims
    /// enrichers.
    /// </summary>
    public IReadOnlyCollection<Claim> Claims { get; init; } = [];
}
