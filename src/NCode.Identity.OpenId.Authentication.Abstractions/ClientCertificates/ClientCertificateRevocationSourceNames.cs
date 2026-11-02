// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Contains the names of the built-in sources of revocation information, the values of
/// <see cref="IClientCertificateRevocationSource.Name"/>. The names are an open vocabulary: a package adds a source by
/// registering an <see cref="IClientCertificateRevocationSource"/> under another name.
/// </summary>
[PublicAPI]
public static class ClientCertificateRevocationSourceNames
{
    /// <summary>
    /// Identifies the source that asks the OCSP responders that the authority information access extension of the
    /// certificate names.
    /// </summary>
    public const string Ocsp = "ocsp";

    /// <summary>
    /// Identifies the source that reads the certificate revocation lists that the CRL distribution points of the
    /// certificate name.
    /// </summary>
    public const string Crl = "crl";
}
