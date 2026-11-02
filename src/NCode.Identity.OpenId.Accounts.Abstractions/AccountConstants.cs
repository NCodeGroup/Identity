// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// Contains constants for local accounts.
/// </summary>
[PublicAPI]
public static class AccountConstants
{
    /// <summary>
    /// The stable, global issuer value for a self-issued local-account connection. Local accounts are global (one per
    /// human, like a federated principal), so the issuer is a fixed constant rather than a per-tenant value — the same
    /// account therefore resolves to the same principal regardless of which tenant it signs in through.
    /// </summary>
    public const string SelfIssuer = "urn:ncode:identity:local-account";
}
