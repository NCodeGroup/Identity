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
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Describes caller-supplied <b>public</b> key material to import into a new secret. The
/// <see cref="ISecretKeyTypeDescriptor"/> for the requested key type parses and validates the material.
/// </summary>
// See ADR-0104.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SecretKeyImportParameters
{
    /// <summary>
    /// Gets the encoding of the supplied <see cref="KeyMaterial"/>.
    /// </summary>
    public required SecretKeyImportFormat Format { get; init; }

    /// <summary>
    /// Gets the caller-supplied public key material, encoded per <see cref="Format"/> (JWK JSON text, PEM text, or
    /// base64 DER for a certificate).
    /// </summary>
    public required string KeyMaterial { get; init; }
}
