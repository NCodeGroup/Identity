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

namespace NCode.Identity.OpenId.Data.Contracts;

/// <summary>
/// Contains the configuration for a cryptographic secret.
/// </summary>
public class Secret : ISupportId
{
    /// <summary>
    /// Gets or sets the surrogate key for this entity.
    /// </summary>
    public required long Id { get; init; }

    /// <summary>
    /// Gets or sets the tenant identifier for this entity.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the natural key for this entity.
    /// Also known as <c>kid</c> or <c>Key ID</c>.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets or sets the intended use for this secret. This property is optional and may be <c>null</c> to
    /// indicate that this secret is intended for use with any compatible algorithm.
    /// Valid values are defined in RFC 7517 Section 4.2:
    /// https://tools.ietf.org/html/rfc7517#section-4.2
    /// </summary>
    public required string? Use { get; init; }

    /// <summary>
    /// Gets or sets the intended algorithm for use with this secret. This property is optional and may be
    /// <c>null</c> to indicate that this secret is intended for use with any compatible algorithm.
    /// </summary>
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
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets or sets the size, in bits, of the key material.
    /// For asymmetric keys, this is the size of the modulus.
    /// For symmetric keys, this is the size of the actual key material.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets or sets the size, in bytes, of the unprotected key material.
    /// For asymmetric keys, this is the size of the key material using the <c>PKCS#8</c> encoding.
    /// For symmetric keys, this is the size of the actual key material.
    /// </summary>
    public required int UnprotectedSizeBytes { get; init; }

    /// <summary>
    /// Gets or sets the protected value of the key material.
    /// The key material is protected using the <c>Data Protection APIs</c> and then <c>base64url</c> encoded.
    /// </summary>
    public required string ProtectedValue { get; init; }
}
