// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Represents an <see cref="SecretKey"/> implementation using <c>symmetric</c> cryptographic keys.
/// </summary>
[PublicAPI]
public abstract class SymmetricSecretKey : SecretKey
{
    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Symmetric;

    /// <summary>
    /// Exports the private key to a <see cref="IBufferWriter{T}"/>.
    /// </summary>
    /// <param name="destination">The destination buffer writer.</param>
    public abstract void ExportPrivateKey<TWriter>(ref TWriter destination)
        where TWriter : IBufferWriter<byte>, allows ref struct;
}
