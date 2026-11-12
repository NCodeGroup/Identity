// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Marks a request whose connection carries the reverse proxy's client certificate.
/// </summary>
/// <remarks>
/// The presence of this feature means the certificate on the connection is the reverse proxy's and never an end
/// client's.
/// </remarks>
[PublicAPI]
public interface IProxyClientCertificateFeature
{
    /// <summary>
    /// Gets the reverse proxy's certificate from the connection.
    /// </summary>
    X509Certificate2 Certificate { get; }
}
