// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Decodes the <see cref="ClientCertificateHeaderFormats.Base64Der"/> format: a comma-separated list of base64-encoded
/// DER certificates, the client's own first — ASP.NET Core's single-certificate forwarding encoding and Traefik's
/// <c>passTLSClientCert</c> chain encoding alike.
/// </summary>
internal sealed class Base64DerClientCertificateHeaderDecoder : IClientCertificateHeaderDecoder
{
    /// <inheritdoc />
    public string Format => ClientCertificateHeaderFormats.Base64Der;

    /// <inheritdoc />
    public bool TryDecode(
        string headerValue,
        [NotNullWhen(true)] out PresentedClientCertificate? certificate
    ) => ClientCertificateDecoding.TryDecodeBase64DerList(headerValue, out certificate);
}
