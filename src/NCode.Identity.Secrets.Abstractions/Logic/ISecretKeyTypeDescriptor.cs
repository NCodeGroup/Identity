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
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Logic;

/// <summary>
/// Describes a single secret key type (for example <c>symmetric</c>, <c>rsa</c>, or <c>ecc</c>) and its intrinsic
/// capabilities. The set of registered descriptors is the single source of truth for which key types exist, how
/// their key sizes are validated, how their material is generated, and which expose exportable public-key material
/// — replacing the per-type switch statements that previously duplicated this knowledge (ADR-0067).
/// </summary>
[PublicAPI]
public interface ISecretKeyTypeDescriptor
{
    /// <summary>
    /// Gets the key-type identifier this descriptor handles. See <see cref="SecretKeyTypes"/>.
    /// </summary>
    string KeyType { get; }

    /// <summary>
    /// Gets the runtime <see cref="SecretKey"/> CLR type that keys of this type are represented by (for example
    /// <see cref="RsaSecretKey"/>). Used to validate algorithm compatibility.
    /// </summary>
    Type KeyClrType { get; }

    /// <summary>
    /// Gets a value indicating whether this key type uses asymmetric cryptography (its <see cref="KeyClrType"/>
    /// derives from <see cref="AsymmetricSecretKey"/>).
    /// </summary>
    bool IsAsymmetric { get; }

    /// <summary>
    /// Gets a value indicating whether keys of this type expose exportable public-key material (JWK and PEM).
    /// Always <c>false</c> for symmetric types.
    /// </summary>
    bool SupportsPublicKeyExport { get; }

    /// <summary>
    /// Validates the requested generation parameters for this key type, returning <c>null</c> when valid or a
    /// human-readable reason when invalid.
    /// </summary>
    /// <param name="parameters">The requested generation parameters.</param>
    /// <returns><c>null</c> when the parameters are valid; otherwise a human-readable rejection reason.</returns>
    string? ValidateGeneration(SecretKeyGenerationParameters parameters);

    /// <summary>
    /// Generates fresh key material for this key type from the requested parameters, invoking
    /// <paramref name="protect"/> to protect the raw private key, and returns the result. Throws when the
    /// parameters are invalid.
    /// </summary>
    /// <param name="parameters">The requested generation parameters.</param>
    /// <param name="protect">Protects the raw private-key bytes.</param>
    /// <returns>The generated key material.</returns>
    GeneratedSecretKeyMaterial GenerateKeyMaterial(
        SecretKeyGenerationParameters parameters,
        SecretKeyMaterialProtector protect
    );
}
