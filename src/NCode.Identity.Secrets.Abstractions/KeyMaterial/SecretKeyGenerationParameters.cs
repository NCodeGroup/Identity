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

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Describes the parameters for generating a new secret key. Required inputs are supplied at construction; optional,
/// key-type-specific inputs (such as <see cref="Curve"/> for elliptic-curve keys) are set via init-only properties
/// so the shape can grow without breaking callers.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SecretKeyGenerationParameters
{
    /// <summary>
    /// Gets the requested key size, in bits. For elliptic-curve keys this is advisory when <see cref="Curve"/> is
    /// specified (the curve determines the actual size).
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the requested elliptic-curve identifier (the <c>JWK</c> <c>crv</c> name, for example <c>P-256</c> or
    /// <c>secp256k1</c>), or <c>null</c> to select a curve by <see cref="KeySizeBits"/>. Only valid for ECC keys.
    /// </summary>
    public string? Curve { get; init; }
}
