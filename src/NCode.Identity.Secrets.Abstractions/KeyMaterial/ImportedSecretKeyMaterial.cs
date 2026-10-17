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
/// Represents the result of importing caller-supplied <b>public</b> key material produced by a
/// <see cref="ISecretKeyTypeDescriptor"/>, with the public key already protected via the supplied
/// <see cref="SecretKeyMaterialProtector"/>.
/// </summary>
// See ADR-0104.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ImportedSecretKeyMaterial
{
    /// <summary>
    /// Gets the opaque protected representation of the public key material, as produced by the
    /// <see cref="SecretKeyMaterialProtector"/>.
    /// </summary>
    public required string ProtectedKeyMaterial { get; init; }

    /// <summary>
    /// Gets the actual size, in bits, of the imported key material.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the encoding of the stored material. See <see cref="SecretEncodings"/> for possible values.
    /// </summary>
    public required string SecretEncoding { get; init; }
}
