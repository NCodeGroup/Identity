// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Decodes the <see cref="ClientCertificateHeaderFormats.UrlEncodedPem"/> format: URL-encoded PEM text of one or more
/// certificates, the client's own first.
/// </summary>
internal sealed class UrlEncodedPemClientCertificateHeaderDecoder : IClientCertificateHeaderDecoder
{
    /// <inheritdoc />
    public string Format => ClientCertificateHeaderFormats.UrlEncodedPem;

    /// <inheritdoc />
    public bool TryDecode(
        string headerValue,
        [NotNullWhen(true)] out PresentedClientCertificate? certificate
    )
    {
        var pem = Uri.UnescapeDataString(headerValue);
        return ClientCertificateDecoding.TryDecodePem(pem, out certificate);
    }
}
