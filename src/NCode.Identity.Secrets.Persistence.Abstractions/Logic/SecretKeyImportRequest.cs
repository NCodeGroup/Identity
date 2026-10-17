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
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Logic;

/// <summary>
/// Contains the metadata and caller-supplied <b>public</b> key material used to import a new
/// <see cref="PersistedSecret"/>. Only public asymmetric material is accepted; private or symmetric material is
/// rejected by the resolved <c>ISecretKeyTypeDescriptor</c>.
/// </summary>
// See ADR-0104.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SecretKeyImportRequest
{
    /// <summary>
    /// Gets the natural identifier to assign to the imported secret.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the secret type (crypto family) to import the material as, which selects the
    /// <c>ISecretKeyTypeDescriptor</c> that parses it. See <c>SecretKeyTypes</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the encoding of the supplied <see cref="KeyMaterial"/>.
    /// </summary>
    public required SecretKeyImportFormat Format { get; init; }

    /// <summary>
    /// Gets the caller-supplied public key material, encoded per <see cref="Format"/> (JWK JSON text, PEM text, or
    /// base64 DER for a certificate).
    /// </summary>
    public required string KeyMaterial { get; init; }

    /// <summary>
    /// Gets the intended use for the imported secret, or <c>null</c> for any compatible use.
    /// See <c>SecretKeyUses</c> for possible values.
    /// </summary>
    public required string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the imported secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public required string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the imported secret is considered created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the imported secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
