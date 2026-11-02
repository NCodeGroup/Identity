// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// The answer of an OCSP responder for one certificate (RFC 6960 §4.2.1), taken from a response without verifying who
/// signed it.
/// </summary>
internal sealed class OcspSingleResponse
{
    /// <summary>
    /// Gets the identifier of the certificate the answer is for.
    /// </summary>
    public required OcspCertificateId CertificateId { get; init; }

    /// <summary>
    /// Gets the status the responder reports: <c>good</c> is <see cref="ClientCertificateRevocationStatus.Good"/>,
    /// <c>revoked</c> is <see cref="ClientCertificateRevocationStatus.Revoked"/> and <c>unknown</c> is
    /// <see cref="ClientCertificateRevocationStatus.Unknown"/>.
    /// </summary>
    public required ClientCertificateRevocationStatus Status { get; init; }

    /// <summary>
    /// Gets the time at which the status is known to be correct.
    /// </summary>
    public required DateTimeOffset ThisUpdate { get; init; }

    /// <summary>
    /// Gets the time at or before which newer information is available, or <see langword="null"/> when the responder
    /// does not say.
    /// </summary>
    public required DateTimeOffset? NextUpdate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the answer carries a critical extension, which this reader does not support, so
    /// that the answer must not be relied on.
    /// </summary>
    public required bool HasUnsupportedFeatures { get; init; }
}
