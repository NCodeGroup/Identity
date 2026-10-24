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

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Contains the options for retrieving and caching the revocation information (OCSP responses and certificate
/// revocation lists) that a mutual-TLS client certificate names.
/// </summary>
public sealed class ClientCertificateRevocationOptions
{
    /// <summary>
    /// Gets the name of the subsection of the OpenID configuration section that binds these options.
    /// </summary>
    public const string SectionName = "ClientCertificateRevocation";

    /// <summary>
    /// Gets or sets the time one retrieval of revocation information (a list or an OCSP response) may take, the body
    /// included. The default is five seconds. It is at least one millisecond and at most about 49.7 days, the periods
    /// a timer accepts.
    /// </summary>
    public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the largest list or OCSP response read, in bytes. A larger one is not used. The default is 10 MiB
    /// (10,485,760 bytes). It is at least one.
    /// </summary>
    public int MaxResponseSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the longest a parsed list or a verified OCSP response is reused before it is retrieved again, even
    /// when its next update is later. The default is one hour. It is greater than zero.
    /// </summary>
    public TimeSpan MaxCacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets how long a retrieval that yielded nothing usable, or a responder location that failed, is
    /// remembered. During that time it is not retried and the certificates it covers rely on the other sources. The
    /// default is one minute. It is greater than zero.
    /// </summary>
    public TimeSpan FailureCacheDuration { get; set; } = TimeSpan.FromMinutes(1);
}
