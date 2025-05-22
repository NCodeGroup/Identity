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
public class PersistedSecret : ISupportResource, ISupportSecretId, ISupportConcurrencyToken
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

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string ConcurrencyToken { get; set; } = string.Empty;

    //

    /// <summary>
    /// Gets or sets the intended use for this secret. This property is optional and may be <c>null</c> to
    /// indicate that this secret is intended for use with any compatible algorithm.
    /// See <see cref="SecretKeyUses"/> for possible values.
    /// </summary>
    [MaxLength(SecretMaxLengths.Use)]
    public required string? Use { get; init; }

    /// <summary>
    /// Gets or sets the intended algorithm for use with this secret. This property is optional and may be
    /// <c>null</c> to indicate that this secret is intended for use with any compatible algorithm.
    /// </summary>
    [MaxLength(SecretMaxLengths.Algorithm)]
    public required string? Algorithm { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when this secret was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when this secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets or sets a value that specifies the type of secret.
    /// See <see cref="SecretTypes"/> for possible values.
    /// </summary>
    [MaxLength(SecretMaxLengths.SecretType)]
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets or sets the size, in bits, of the key material.
    /// For asymmetric keys, this is the size of the modulus.
    /// For symmetric keys, this is the size of the actual key material.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets or sets any identifying information about how the key material is encoded, encrypted, versioned, etc.
    /// See <see cref="SecretEncodingTypes"/> for possible values.
    /// </summary>
    [MaxLength(SecretMaxLengths.EncodingType)]
    public required string EncodingType { get; init; }

    /// <summary>
    /// Gets or sets the encoded value of the key material.
    /// Any details about how the key material is encoded, encrypted, versioned, etc.
    /// must be described in the <see cref="EncodingType"/> property.
    /// </summary>
    public required string EncodedValue { get; init; }
}
