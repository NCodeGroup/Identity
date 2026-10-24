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
/// Specifies the outcome of checking whether an issuer has revoked a client certificate.
/// </summary>
/// <remarks>
/// The members are the outcomes of a revocation check, so the set is closed. The zero value is
/// <see cref="Unknown"/>, so an unset or defaulted status fails closed.
/// </remarks>
[PublicAPI]
public enum ClientCertificateRevocationStatus
{
    /// <summary>
    /// The revocation status could not be determined, and the certificate is treated as not trusted.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The issuer has not revoked the certificate.
    /// </summary>
    Good = 1,

    /// <summary>
    /// The issuer has revoked the certificate.
    /// </summary>
    Revoked = 2,
}
