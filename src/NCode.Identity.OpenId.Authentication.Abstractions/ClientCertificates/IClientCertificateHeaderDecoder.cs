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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Decodes the mutual-TLS client certificate a reverse proxy, load balancer, or service mesh forwards in a request
/// header, for one named format. Decoders are a strategy collection resolved by <see cref="Format"/>: the default
/// <see cref="IClientCertificateResolver"/> uses the one named by <see cref="MutualTlsForwardingOptions.Format"/>, and a
/// package supports another proxy's encoding by registering a decoder for it. Two decoders never share a format name.
/// </summary>
[PublicAPI]
public interface IClientCertificateHeaderDecoder
{
    /// <summary>
    /// Gets the name of the format this decoder reads, compared ordinally. The known names are the values of
    /// <see cref="ClientCertificateHeaderFormats"/>.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Decodes a forwarded header value into the certificate the client presented and the other certificates it sent
    /// with it. A value that does not conform to the format in full — including one that holds no certificate, or any
    /// part that is not a certificate — is not decoded at all.
    /// </summary>
    /// <param name="headerValue">The non-empty value of the forwarded header.</param>
    /// <param name="certificate">When this method returns <see langword="true"/>, the decoded
    /// <see cref="PresentedClientCertificate"/>; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value conforms to the format; otherwise, <see langword="false"/>.</returns>
    bool TryDecode(
        string headerValue,
        [NotNullWhen(true)] out PresentedClientCertificate? certificate
    );
}
