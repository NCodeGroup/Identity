// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.AspNetCore.ProxyClientCertificate;

/// <summary>
/// The reason <see cref="ProxyClientCertificateVerifier"/> refuses a certificate as the reverse proxy's. The checks are
/// fixed by this package, so the set is closed.
/// </summary>
internal enum ProxyClientCertificateRefusal
{
    /// <summary>
    /// The certificate is the proxy's: no check refused it.
    /// </summary>
    None = 0,

    /// <summary>
    /// The certificate's validity period has not started or has ended.
    /// </summary>
    NotYetValidOrExpired,

    /// <summary>
    /// The certificate does not have the client authentication extended key usage.
    /// </summary>
    MissingClientAuthUsage,

    /// <summary>
    /// The certificate's subject is not one of the allowed subjects.
    /// </summary>
    SubjectNotAllowed,

    /// <summary>
    /// The certificate does not chain to the configured certificate authorities.
    /// </summary>
    UnanchoredChain,
}
