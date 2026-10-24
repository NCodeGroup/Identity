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
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// The well-known encodings of caller-supplied <b>public</b> key material presented to a
/// <see cref="ISecretKeyTypeDescriptor"/> for import.
/// </summary>
/// <remarks>
/// The formats are an open vocabulary (ADR-0150): each key type declares the formats it parses in
/// <see cref="ISecretKeyTypeDescriptor.PublicKeyImportFormats"/>, so a key type registered by another package can accept
/// its own format, such as an OpenSSH public key. Names are compared ordinally.
/// </remarks>
// See ADR-0104.
[PublicAPI]
public static class SecretKeyImportFormats
{
    /// <summary>
    /// The material is a single <c>JSON Web Key</c> (RFC 7517) object carrying only public members.
    /// </summary>
    public const string Jwk = "jwk";

    /// <summary>
    /// The material is a <c>PEM</c>-encoded <c>SubjectPublicKeyInfo</c> public key
    /// (<c>-----BEGIN PUBLIC KEY-----</c>).
    /// </summary>
    public const string Pem = "pem";

    /// <summary>
    /// The material is an <c>X.509</c> certificate, either <c>PEM</c>-encoded
    /// (<c>-----BEGIN CERTIFICATE-----</c>) or base64 DER.
    /// </summary>
    public const string Certificate = "certificate";
}
