// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Contains a key that was wrapped by a key wrapper, together with the name of the wrapper and the identifier of the
/// key encryption key that are needed to unwrap it later.
/// </summary>
// See ADR-1260.
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
    /// A wrapper that stores binary ciphertext as base64url text can decode it with <see cref="WrappedKeyCiphertext"/>.
    /// </summary>
    public required string Ciphertext { get; init; }
}
