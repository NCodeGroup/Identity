// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.DataContracts;

/// <summary>
/// Contains the data for a single profile claim owned by a <see cref="PersistedLocalAccount"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedLocalAccountClaim
{
    /// <summary>
    /// Gets the claim type.
    /// </summary>
    [MaxLength(AccountMaxLengths.ClaimType)]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the claim value.
    /// </summary>
    [MaxLength(AccountMaxLengths.ClaimValue)]
    public required string Value { get; init; }
}
