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

using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Provides the ability to resolve the mutual-TLS client certificate presented for the current request, abstracting
/// over how TLS is terminated. The default reads the certificate negotiated on the connection
/// (<see cref="ConnectionInfo.ClientCertificate"/>); production deployments that terminate TLS at a reverse proxy,
/// load balancer, or service mesh populate that property from a trusted forwarded header (for example via ASP.NET
/// Core <c>AddCertificateForwarding</c>) so this seam needs no deployment-specific logic. Replace it only for a
/// bespoke certificate source.
/// </summary>
[PublicAPI]
public interface IClientCertificateResolver
{
    /// <summary>
    /// Resolves the client certificate presented for the current request, or <see langword="null"/> when none was
    /// presented.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The resolved <see cref="X509Certificate2"/>, or <see langword="null"/> when none was presented.</returns>
    ValueTask<X509Certificate2?> ResolveCertificateAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    );
}
