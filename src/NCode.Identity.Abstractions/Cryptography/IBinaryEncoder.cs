// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cryptography;

/// <summary>
/// Encodes binary data as text under one registered encoding name.
/// </summary>
/// <remarks>
/// Register an implementation in the <see cref="IBinaryEncoder"/> strategy collection to add an encoding;
/// <see cref="ICryptoService"/> resolves encoders by <see cref="Name"/> and rejects a duplicate or unknown name.
/// </remarks>
[PublicAPI]
public interface IBinaryEncoder
{
    /// <summary>
    /// Gets the name of the encoding, such as a <see cref="BinaryEncodings"/> name. Names compare ordinally.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Encodes binary data as text.
    /// </summary>
    /// <param name="data">The binary data to encode.</param>
    /// <returns>The binary data encoded as text.</returns>
    string Encode(ReadOnlySpan<byte> data);
}
