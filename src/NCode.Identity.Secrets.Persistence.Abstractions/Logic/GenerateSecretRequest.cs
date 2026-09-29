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
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Logic;

/// <summary>
/// Contains the metadata used to generate a new <see cref="PersistedSecret"/> whose key material is created
/// server-side and never supplied by the caller.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class GenerateSecretRequest
{
    /// <summary>
    /// Gets the natural identifier to assign to the generated secret.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the type of secret to generate. See <see cref="SecretTypes"/> for the supported values;
    /// <see cref="SecretTypes.Certificate"/> cannot be generated.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key material to generate. For symmetric secrets this is the size of the
    /// random key; for <c>RSA</c> the modulus size; for <c>ECC</c> the curve size (256, 384, or 521).
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the intended use for the generated secret, or <c>null</c> for any compatible use.
    /// See <c>SecretKeyUses</c> for possible values.
    /// </summary>
    public required string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the generated secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public required string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the generated secret is considered created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the generated secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    // Future: an optional caller-supplied key-material field (import / BYOK) may be added here as an
    // additive change once its validation and threat model are reviewed. See ADR-0011.
}
