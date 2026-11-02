// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.Credentials;

/// <summary>
/// Contains options for the default PBKDF2 password hasher.
/// </summary>
[PublicAPI]
public sealed class PasswordHasherOptions
{
    /// <summary>
    /// Gets or sets the number of PBKDF2 iterations. The default is <c>600000</c>, the OWASP recommendation for
    /// PBKDF2-HMAC-SHA256. A stored hash produced with fewer iterations than this is flagged for rehashing on the next
    /// successful verification.
    /// </summary>
    public int Iterations { get; set; } = 600_000;

    /// <summary>
    /// Gets or sets the length, in bytes, of the randomly-generated salt. The default is <c>16</c>.
    /// </summary>
    public int SaltByteCount { get; set; } = 16;

    /// <summary>
    /// Gets or sets the length, in bytes, of the derived hash. The default is <c>32</c>.
    /// </summary>
    public int HashByteCount { get; set; } = 32;
}
