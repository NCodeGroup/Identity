// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides a delegate to perform the calculation of a <c>keyed hash (HMAC)</c> function.
/// </summary>
[PublicAPI]
public delegate bool KeyedHashFunctionDelegate(
    ReadOnlySpan<byte> key,
    ReadOnlySpan<byte> source,
    Span<byte> destination,
    out int bytesWritten
);
