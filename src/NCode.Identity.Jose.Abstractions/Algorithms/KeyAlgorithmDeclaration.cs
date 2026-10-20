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

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes a key together with the use and the algorithm declared for it, which
/// <see cref="AlgorithmCollectionExtensions"/> checks for coherence against the registered algorithms.
/// </summary>
[PublicAPI]
public readonly record struct KeyAlgorithmDeclaration
{
    /// <summary>
    /// Gets the runtime <see cref="Type"/> of the key (for example the <c>RsaSecretKey</c> type).
    /// </summary>
    public required Type KeyType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the declared use of the key (<c>sig</c> or <c>enc</c>), or <c>null</c> when none is declared.
    /// </summary>
    public required string? Use { get; init; }

    /// <summary>
    /// Gets the code of the algorithm declared for the key.
    /// </summary>
    public required string Algorithm { get; init; }
}
