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
using JetBrains.Annotations;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted secret.
/// </summary>
[PublicAPI]
public class PersistedSecret : ISupportId, ISupportTenantId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// A value of <c>0</c> indicates that this entity has not been persisted to storage yet.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets or sets the tenant identifier for this entity.
    /// </summary>
    [MaxLength(MaxLengths.TenantId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the natural key for this entity.
    /// Also known as <c>kid</c> or <c>Key ID</c>.
    /// </summary>
    [MaxLength(MaxLengths.SecretId)]
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets or sets a random value that is used to check for optimistic concurrency violations.
    /// </summary>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; init; }

    /// <summary>
    /// Gets or sets the intended use for this secret. This property is optional and may be <c>null</c> to
    /// indicate that this secret is intended for use with any compatible algorithm.
    /// See <see cref="SecretKeyUses"/> for possible values.
    /// </summary>
    [MaxLength(MaxLengths.SecretUse)]
    public required string? Use { get; init; }

    /// <summary>
    /// Gets or sets the intended algorithm for use with this secret. This property is optional and may be
    /// <c>null</c> to indicate that this secret is intended for use with any compatible algorithm.
    /// </summary>
    [MaxLength(MaxLengths.SecretAlgorithm)]
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
    [MaxLength(MaxLengths.SecretType)]
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
    public required string EncodingType { get; init; }

    /// <summary>
    /// Gets or sets the encoded value of the key material.
    /// Any details about how the key material is encoded, encrypted, versioned, etc.
    /// must be described in the <see cref="EncodingType"/> property.
    /// </summary>
    public required string EncodedValue { get; init; }
}
