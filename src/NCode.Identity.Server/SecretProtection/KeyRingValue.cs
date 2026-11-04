// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// A protected value that a Data Protection key stores, with the instant the key expires.
/// </summary>
/// <param name="ProtectedValue">The protected value, which names the master key that protects it.</param>
/// <param name="ExpirationDate">The instant the key expires, or <see langword="null"/> when it is unknown, in which
/// case the key counts as live.</param>
// See ADR-1407.
internal sealed record KeyRingValue(string ProtectedValue, DateTimeOffset? ExpirationDate)
{
    /// <summary>
    /// Determines whether the key is still needed: it has not expired, or it expired within the retirement grace, which
    /// covers the payloads it protected just before it expired.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <param name="retirementGrace">How long after its expiration a key is still needed.</param>
    /// <returns><see langword="true"/> when the key is live.</returns>
    public bool IsLive(DateTimeOffset now, TimeSpan retirementGrace) =>
        ExpirationDate is not { } expirationDate || expirationDate + retirementGrace > now;
}
