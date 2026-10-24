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

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the request body to import a caller-supplied <b>public</b> asymmetric key as a new secret. Only public
/// material is accepted; a private or symmetric key is rejected. The secret type and key size are derived from the
/// supplied material.
/// </summary>
// See ADR-0104.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ImportSecretRequest
{
    /// <summary>
    /// Gets the secret type (crypto family) to import the material as. Supported values are <c>rsa</c> and
    /// <c>ecc</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the encoding of the supplied <see cref="KeyMaterial"/>: a lowercase format name the secret type imports.
    /// The built-in RSA and ECC types import <c>jwk</c>, <c>pem</c>, and <c>certificate</c>.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    /// Gets the caller-supplied public key material, encoded per <see cref="Format"/> (JWK JSON text, PEM text, or
    /// base64 DER for a certificate).
    /// </summary>
    public required string KeyMaterial { get; init; }

    /// <summary>
    /// Gets the intended use for the imported secret, or <c>null</c> for any compatible use.
    /// </summary>
    public string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the imported secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the imported secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
