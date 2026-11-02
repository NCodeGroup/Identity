// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Decodes the <see cref="ClientCertificateHeaderFormats.Pem"/> format: the PEM text of one or more certificates, the
/// client's own first.
/// </summary>
internal sealed class PemClientCertificateHeaderDecoder : IClientCertificateHeaderDecoder
{
    /// <inheritdoc />
    public string Format => ClientCertificateHeaderFormats.Pem;

    /// <inheritdoc />
    public bool TryDecode(
        string headerValue,
        [NotNullWhen(true)] out PresentedClientCertificate? certificate
    ) => ClientCertificateDecoding.TryDecodePem(headerValue, out certificate);
}
