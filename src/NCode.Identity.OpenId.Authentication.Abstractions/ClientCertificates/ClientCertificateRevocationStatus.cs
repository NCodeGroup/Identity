// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Specifies the outcome of checking whether an issuer has revoked a client certificate.
/// </summary>
/// <remarks>
/// The members are the outcomes of a revocation check, so the set is closed. The zero value is
/// <see cref="Unknown"/>, so an unset or defaulted status fails closed.
/// </remarks>
[PublicAPI]
public enum ClientCertificateRevocationStatus
{
    /// <summary>
    /// The revocation status could not be determined, and the certificate is treated as not trusted.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The issuer has not revoked the certificate.
    /// </summary>
    Good = 1,

    /// <summary>
    /// The issuer has revoked the certificate.
    /// </summary>
    Revoked = 2,
}
