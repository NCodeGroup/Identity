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

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Contains a key that was wrapped by a key wrapper, together with the name of the wrapper and the identifier of the
/// key encryption key that are needed to unwrap it later.
/// </summary>
// See ADR-0278.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class WrappedKey
{
    /// <summary>
    /// Gets the name of the key wrapper that wrapped the key, such as one of the values in
    /// <see cref="KeyWrapperNames"/>.
    /// </summary>
    public required string WrapperName { get; init; }

    /// <summary>
    /// Gets the identifier of the key encryption key that wrapped the key, as reported by the wrapper.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the wrapped key as a string that is safe to store. The format is defined by the wrapper.
    /// </summary>
    public required string Ciphertext { get; init; }
}
