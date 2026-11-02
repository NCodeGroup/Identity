// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.Management.Contracts;

/// <summary>
/// Represents the request body to replace a local account's full set of profile claims. The supplied claims replace
/// the current set in its entirety.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SetLocalAccountClaimsRequest
{
    /// <summary>
    /// Gets the replacement profile claims.
    /// </summary>
    public required IReadOnlyList<LocalAccountClaim> Claims { get; init; }
}
