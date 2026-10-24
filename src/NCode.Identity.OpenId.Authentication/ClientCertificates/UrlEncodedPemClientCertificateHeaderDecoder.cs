#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
