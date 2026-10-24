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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Contains the names of the known formats in which a reverse proxy, load balancer, or service mesh forwards the
/// mutual-TLS client certificate in a request header — the values of <see cref="MutualTlsForwardingOptions.Format"/>.
/// The formats are an open vocabulary: each is the name of a registered <see cref="IClientCertificateHeaderDecoder"/>,
/// and a package adds a format by registering a decoder for it. These are the formats registered by default, not all
/// of them.
/// </summary>
[PublicAPI]
public static class ClientCertificateHeaderFormats
{
    /// <summary>
    /// Identifies the format in which the header value is a comma-separated list of base64-encoded DER certificates,
    /// the client's own certificate first. A single certificate is ASP.NET Core's certificate-forwarding encoding; a
    /// list is Traefik's <c>passTLSClientCert</c> with <c>pem: true</c>, which strips the PEM delimiters and line breaks
    /// from each certificate of the chain the client presented.
    /// </summary>
    public const string Base64Der = nameof(Base64Der);

    /// <summary>
    /// Identifies the format in which the header value is the PEM text of one or more certificates, the client's own
    /// certificate first.
    /// </summary>
    public const string Pem = nameof(Pem);

    /// <summary>
    /// Identifies the format in which the header value is URL-encoded PEM text of one or more certificates, the
    /// client's own certificate first (for example nginx <c>$ssl_client_escaped_cert</c>, or the AWS Application Load
    /// Balancer <c>X-Amzn-Mtls-Clientcert</c> header, which carries the whole chain in pass-through mode).
    /// </summary>
    public const string UrlEncodedPem = nameof(UrlEncodedPem);

    /// <summary>
    /// Identifies the format in which the header value is an Envoy or Istio <c>X-Forwarded-Client-Cert</c> (XFCC) list:
    /// the element added by the proxy closest to the client carries its certificate as URL-encoded PEM in <c>Cert</c>,
    /// or its whole chain in <c>Chain</c>.
    /// </summary>
    public const string EnvoyXfcc = nameof(EnvoyXfcc);
}
