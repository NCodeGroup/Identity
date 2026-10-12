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

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes an inclusive range of legal key sizes, in bits, mirroring <see cref="System.Security.Cryptography.KeySizes"/>.
/// A <see cref="SkipBits"/> of <c>0</c> denotes a single fixed size equal to <see cref="MinBits"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class KeyBitSizesResource
{
    /// <summary>
    /// Gets the smallest legal key size, in bits.
    /// </summary>
    public required int MinBits { get; init; }

    /// <summary>
    /// Gets the largest legal key size, in bits.
    /// </summary>
    public required int MaxBits { get; init; }

    /// <summary>
    /// Gets the increment, in bits, between legal key sizes. A value of <c>0</c> denotes a single fixed size.
    /// </summary>
    public required int SkipBits { get; init; }
}
