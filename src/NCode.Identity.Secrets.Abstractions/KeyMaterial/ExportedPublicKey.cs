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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Represents the exported <b>public</b> material of an asymmetric secret key, produced by a
/// <see cref="ISecretKeyTypeDescriptor"/> — a <c>JSON Web Key</c> (RFC 7517) object and a
/// <c>SubjectPublicKeyInfo</c> PEM. No private key material is included.
/// </summary>
// See ADR-0104.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ExportedPublicKey
{
    /// <summary>
    /// Gets the public key as a <c>JSON Web Key</c> (RFC 7517) object carrying the key-type-specific members
    /// (for example <c>kty</c>/<c>n</c>/<c>e</c> or <c>kty</c>/<c>crv</c>/<c>x</c>/<c>y</c>) plus any
    /// <c>kid</c>/<c>use</c>/<c>alg</c> from the key's metadata.
    /// </summary>
    public required JsonObject Jwk { get; init; }

    /// <summary>
    /// Gets the public key as a <c>SubjectPublicKeyInfo</c> PEM (<c>-----BEGIN PUBLIC KEY-----</c>).
    /// </summary>
    public required string Pem { get; init; }
}
