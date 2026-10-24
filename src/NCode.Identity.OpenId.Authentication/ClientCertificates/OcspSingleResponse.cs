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
/// The answer of an OCSP responder for one certificate (RFC 6960 §4.2.1), taken from a response without verifying who
/// signed it.
/// </summary>
internal sealed class OcspSingleResponse
{
    /// <summary>
    /// Gets the identifier of the certificate the answer is for.
    /// </summary>
    public required OcspCertificateId CertificateId { get; init; }

    /// <summary>
    /// Gets the status the responder reports: <c>good</c> is <see cref="ClientCertificateRevocationStatus.Good"/>,
    /// <c>revoked</c> is <see cref="ClientCertificateRevocationStatus.Revoked"/> and <c>unknown</c> is
    /// <see cref="ClientCertificateRevocationStatus.Unknown"/>.
    /// </summary>
    public required ClientCertificateRevocationStatus Status { get; init; }

    /// <summary>
    /// Gets the time at which the status is known to be correct.
    /// </summary>
    public required DateTimeOffset ThisUpdate { get; init; }

    /// <summary>
    /// Gets the time at or before which newer information is available, or <see langword="null"/> when the responder
    /// does not say.
    /// </summary>
    public required DateTimeOffset? NextUpdate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the answer carries a critical extension, which this reader does not support, so
    /// that the answer must not be relied on.
    /// </summary>
    public required bool HasUnsupportedFeatures { get; init; }
}
