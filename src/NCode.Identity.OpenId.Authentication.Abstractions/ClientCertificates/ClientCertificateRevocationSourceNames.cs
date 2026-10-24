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
/// Contains the names of the built-in sources of revocation information, the values of
/// <see cref="IClientCertificateRevocationSource.Name"/>. The names are an open vocabulary: a package adds a source by
/// registering an <see cref="IClientCertificateRevocationSource"/> under another name.
/// </summary>
[PublicAPI]
public static class ClientCertificateRevocationSourceNames
{
    /// <summary>
    /// Identifies the source that asks the OCSP responders that the authority information access extension of the
    /// certificate names.
    /// </summary>
    public const string Ocsp = "ocsp";

    /// <summary>
    /// Identifies the source that reads the certificate revocation lists that the CRL distribution points of the
    /// certificate name.
    /// </summary>
    public const string Crl = "crl";
}
