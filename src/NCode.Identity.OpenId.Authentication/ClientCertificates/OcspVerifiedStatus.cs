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
/// The status an OCSP response reports for a certificate, after the response was parsed, found current and verified
/// as signed by the issuer or by a responder the issuer authorized.
/// </summary>
/// <param name="Status">The status the responder reports for the certificate.</param>
/// <param name="UncheckedResponder">
/// The encoded certificate of a delegated responder whose own revocation the caller still has to check, or an empty
/// value when the issuer signed the response or the responder carries the <c>id-pkix-ocsp-nocheck</c> extension.
/// </param>
internal sealed record OcspVerifiedStatus(
    ClientCertificateRevocationStatus Status,
    ReadOnlyMemory<byte> UncheckedResponder
);
