// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.Management.Contracts;

/// <summary>
/// Represents the request body to reset a local account's credential. Resetting the credential ends every session of
/// the account's end user and revokes the tokens issued to them.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ResetLocalAccountPasswordRequest
{
    /// <summary>
    /// Gets the account's new password, hashed server-side.
    /// </summary>
    public required string Password { get; init; }
}
