#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.Secrets.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted secret.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedSecret : ISupportResource, ISupportSecretId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the prefix for the resource type.
    /// </summary>
    [MaxLength(MaxLengths.ResourceType)]
    public const string ResourceTypePrefix = SecretResourceTypes.Secret;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public string ResourceType => ResourceTypePrefix;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public string ResourceId => SecretId;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the key identifier (<c>kid</c>) the key is selected by, or <c>null</c> when the key is identified by its
    /// <see cref="SecretId"/>. A key registered by its owner (for example a client's public key) keeps the
    /// <c>kid</c> it was registered with, which is unique among its owner's keys but not across owners; a key the
    /// server generates is identified by its <see cref="SecretId"/>.
    /// </summary>
    [MaxLength(SecretMaxLengths.KeyId)]
    public string? KeyId { get; init; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string ConcurrencyToken { get; set; } = string.Empty;

    //

    /// <summary>
    /// Gets the intended use for this secret. This property is optional and may be <c>null</c> to
    /// indicate that this secret is intended for use with any compatible algorithm.
    /// See <see cref="SecretKeyUses"/> for possible values.
    /// </summary>
    [MaxLength(SecretMaxLengths.Use)]
    public required string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for use with this secret. This property is optional and may be
    /// <c>null</c> to indicate that this secret is intended for use with any compatible algorithm.
    /// </summary>
    [MaxLength(SecretMaxLengths.Algorithm)]
    public required string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when this secret was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when this secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets a value that specifies the type of secret.
    /// See <see cref="SecretTypes"/> for possible values.
    /// </summary>
    [MaxLength(SecretMaxLengths.SecretType)]
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets a value that specifies how the stored key material is encoded — an axis orthogonal to
    /// <see cref="SecretType"/> that distinguishes private (<c>PKCS#8</c>) from public (SPKI) asymmetric material.
    /// See <see cref="SecretEncodings"/> for possible values.
    /// </summary>
    // See ADR-0104.
    [MaxLength(SecretMaxLengths.SecretEncoding)]
    public required string SecretEncoding { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key material.
    /// For asymmetric keys, this is the size of the modulus.
    /// For symmetric keys, this is the size of the actual key material.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the base64url encoded value of the key material.
    /// </summary>
    [MaxLength(SecretMaxLengths.EncodedValue)]
    public required string EncodedValue { get; init; }

    /// <summary>
    /// Gets the base64url-encoded <c>SubjectPublicKeyInfo</c> DER of the public half of a private asymmetric key, so
    /// the public key is read without unprotecting <see cref="EncodedValue"/>; or <c>null</c> when the secret holds
    /// no private asymmetric key (its <see cref="EncodedValue"/> is symmetric or already public material).
    /// </summary>
    // See ADR-0268.
    [MaxLength(SecretMaxLengths.PublicKey)]
    public string? PublicKey { get; init; }
}
