// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Accounts.Management;

/// <summary>
/// Pairs a local account with the identifier of the principal that owns it, so the management endpoints can answer
/// with the principal while the response's entity tag still derives from the account's concurrency token.
/// </summary>
/// <param name="Account">The local account.</param>
/// <param name="PrincipalId">
/// The identifier of the principal that owns the account, or <c>null</c> when none is linked.
/// </param>
internal sealed record LocalAccountWithPrincipal(PersistedLocalAccount Account, string? PrincipalId)
    : ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the concurrency token of the <see cref="Account"/>.
    /// </summary>
    public string ConcurrencyToken => Account.ConcurrencyToken;
}
