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
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

/// <summary>
/// Represents an entity framework data contract for an <c>OAuth</c> or <c>OpenID Connect</c> secret.
/// The complimentary DTO for this entity is <see cref="PersistedSecret"/>.
/// </summary>
[Index(nameof(TenantId), nameof(NormalizedSecretId), IsUnique = true)]
public class SecretEntity : ISupportId, ISupportConcurrencyToken
{
    /// <inheritdoc />
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <summary>
    /// Gets or sets the foreign key for the associated tenant.
    /// </summary>
    [ForeignKey(nameof(Tenant))]
    public required long? TenantId { get; init; }

    /// <summary>
    /// Gets or sets the natural key for this entity.
    /// Also known as <c>kid</c> or <c>Key ID</c>.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.SecretId)]
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets or sets the value of <see cref="SecretId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.SecretId)]
    public required string NormalizedSecretId { get; init; }

    //

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; init; }

    /// <summary>
    /// Gets or sets the intended use for this secret. This property is optional and may be <c>null</c> to
    /// indicate that this secret is intended for use with any compatible algorithm.
    /// Valid values are defined in RFC 7517 Section 4.2:
    /// https://tools.ietf.org/html/rfc7517#section-4.2
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.SecretUse)]
    public required string? Use { get; init; }

    /// <summary>
    /// Gets or sets the intended algorithm for use with this secret. This property is optional and may be
    /// <c>null</c> to indicate that this secret is intended for use with any compatible algorithm.
    /// </summary>
    [Unicode(false)]
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
    [Unicode(false)]
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
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.EncodingType)]
    public required string EncodingType { get; init; }

    /// <summary>
    /// Gets or sets the encoded value of the key material.
    /// Any details about how the key material is encoded, encrypted, versioned, etc.
    /// must be described in the <see cref="EncodingType"/> property.
    /// </summary>
    [Unicode(false)]
    [MaxLength(-1)]
    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
    // We never know how long the value will be.
    public required string EncodedValue { get; init; }

    // navigation properties

    // ReSharper disable once EntityFramework.ModelValidation.CircularDependency
    // We use DTOs to avoid circular dependencies.
    /// <summary>
    /// Gets or sets the navigation property for the associated tenant.
    /// </summary>
    public required TenantEntity? Tenant { get; init; }
}
