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

using System.Collections.Immutable;
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Contains the mutual-TLS client certificate a client presented for a request, with the other certificates it sent
/// alongside. Nothing about it is trusted: it is what the TLS terminator received, so a consumer that relies on the
/// certificate for more than proof of possession validates it first.
/// </summary>
/// <param name="certificate">The client's own (leaf) certificate.</param>
[PublicAPI]
public sealed class PresentedClientCertificate(X509Certificate2 certificate)
{
    /// <summary>
    /// Gets the client's own (leaf) certificate: the certificate whose private key the client proved possession of in
    /// the TLS handshake.
    /// </summary>
    public X509Certificate2 Certificate { get; } = certificate;

    /// <summary>
    /// Gets the other certificates the client presented with <see cref="Certificate"/>, in the order it sent them —
    /// usually the intermediate certificate authorities that issued it. They are untrusted hints for building a
    /// certificate chain and never a trust anchor. Empty when the client sent only its own certificate or the
    /// certificate source does not expose the rest of the chain.
    /// </summary>
    public ImmutableArray<X509Certificate2> Intermediates { get; init; } = [];
}
