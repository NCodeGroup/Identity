#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Identifies how a reverse proxy, load balancer, or service mesh encodes the forwarded mutual-TLS client certificate
/// in the header named by <see cref="MutualTlsForwardingOptions.CertificateHeaderName"/>.
/// </summary>
[PublicAPI]
public enum MutualTlsClientCertificateFormat
{
    /// <summary>
    /// The header value is the base64-encoded DER of the certificate (ASP.NET Core's default forwarding encoding).
    /// </summary>
    Base64Der = 0,

    /// <summary>
    /// The header value is the PEM text of the certificate.
    /// </summary>
    Pem,

    /// <summary>
    /// The header value is URL-encoded PEM text (for example nginx <c>$ssl_client_escaped_cert</c> or the AWS ALB
    /// <c>X-Amzn-Mtls-Clientcert</c> header).
    /// </summary>
    UrlEncodedPem,

    /// <summary>
    /// The header value is an Envoy/Istio <c>X-Forwarded-Client-Cert</c> (XFCC) list whose <c>Cert</c> element carries
    /// URL-encoded PEM.
    /// </summary>
    EnvoyXfcc,
}
