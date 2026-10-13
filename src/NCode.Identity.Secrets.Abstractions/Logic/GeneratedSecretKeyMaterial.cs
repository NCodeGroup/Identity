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

namespace NCode.Identity.Secrets.Logic;

/// <summary>
/// Represents freshly-generated key material produced by a <see cref="ISecretKeyTypeDescriptor"/>, with the
/// private key already protected via the supplied <see cref="SecretKeyMaterialProtector"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class GeneratedSecretKeyMaterial
{
    /// <summary>
    /// Gets the opaque protected representation of the private key, as produced by the
    /// <see cref="SecretKeyMaterialProtector"/>.
    /// </summary>
    public required string ProtectedKeyMaterial { get; init; }

    /// <summary>
    /// Gets the actual size, in bits, of the generated key material (which may be normalized from the request).
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the one-time unprotected key material, base64url-encoded, or <c>null</c> when the key has no
    /// externally-held material to reveal. Populated only for symmetric keys; asymmetric private keys are never
    /// surfaced.
    /// </summary>
    // See ADR-0050.
    public required string? SecretMaterial { get; init; }
}
