// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.Management.Contracts;

/// <summary>
/// Represents a single profile claim of a local account: an individually-addressable <c>type</c>/<c>value</c>
/// assertion (such as <c>given_name</c> or <c>email</c>) that contributes to issued tokens and the UserInfo response.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class LocalAccountClaim
{
    /// <summary>
    /// Gets the claim type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the claim value.
    /// </summary>
    public required string Value { get; init; }
}
