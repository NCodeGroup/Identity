// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// The outcome of checking a certificate against one CRL distribution point: whether a list at the point revokes the
/// certificate, and the reasons the point answered for.
/// </summary>
/// <param name="IsRevoked">Whether a list at the point revokes the certificate.</param>
/// <param name="CoveredReasons">The reasons the point answered for as a mask, zero when it answered for none.</param>
internal readonly record struct CrlPointOutcome(bool IsRevoked, int CoveredReasons)
{
    /// <summary>
    /// Gets the outcome of a point whose list revokes the certificate.
    /// </summary>
    public static CrlPointOutcome Revoked { get; } = new(true, 0);

    /// <summary>
    /// Gets the outcome of a point that answered for no reason that was not already answered for.
    /// </summary>
    public static CrlPointOutcome Uncovered { get; } = new(false, 0);
}
