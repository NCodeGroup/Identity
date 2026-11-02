// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.Credentials;

/// <summary>
/// Describes the outcome of verifying a password against a stored hash.
/// </summary>
[PublicAPI]
public enum PasswordVerificationResult
{
    /// <summary>
    /// The password did not match the stored hash.
    /// </summary>
    Failed = 0,

    /// <summary>
    /// The password matched the stored hash.
    /// </summary>
    Success = 1,

    /// <summary>
    /// The password matched, but the stored hash uses weaker parameters than the current configuration and should be
    /// rehashed and re-stored.
    /// </summary>
    SuccessRehashNeeded = 2,
}
