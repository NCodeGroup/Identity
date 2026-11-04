// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Reads and writes public COSE_Key and COSE_KeySet structures (RFC 9052 §7) through the registered secret key types.
/// The converter handles public keys only: a COSE_Key that carries private key material is refused, and a key with no
/// public form is never written.
/// </summary>
// See ADR-1409.
[PublicAPI]
public interface ICoseKeyConverter
{
    /// <summary>
    /// Reads one public COSE_Key.
    /// </summary>
    /// <param name="encodedKey">The encoded COSE_Key map.</param>
    /// <returns>The key.</returns>
    /// <exception cref="CoseException">The key is malformed, carries private key material, is of an unregistered key
    /// type or curve, or cannot be imported.</exception>
    SecretKey ReadPublicKey(ReadOnlyMemory<byte> encodedKey);

    /// <summary>
    /// Reads a COSE_KeySet. A key of an unregistered key type, curve or algorithm is skipped, as a JSON Web Key Set
    /// reader skips a key it cannot use; any other failure is thrown.
    /// </summary>
    /// <param name="encodedKeySet">The encoded array of COSE_Key maps.</param>
    /// <returns>The keys that can be used.</returns>
    /// <exception cref="CoseException">The key set or one of its keys is malformed.</exception>
    IReadOnlyList<SecretKey> ReadPublicKeySet(ReadOnlyMemory<byte> encodedKeySet);

    /// <summary>
    /// Writes the public half of a key as a COSE_Key.
    /// </summary>
    /// <param name="secretKey">The key.</param>
    /// <returns>The encoded COSE_Key map.</returns>
    /// <exception cref="ArgumentException">The key has no public form or is of a key type that is not registered.</exception>
    byte[] WritePublicKey(SecretKey secretKey);

    /// <summary>
    /// Writes the public halves of keys as a COSE_KeySet.
    /// </summary>
    /// <param name="secretKeys">The keys.</param>
    /// <returns>The encoded array of COSE_Key maps.</returns>
    /// <exception cref="ArgumentException">A key has no public form or is of a key type that is not registered.</exception>
    byte[] WritePublicKeySet(IEnumerable<SecretKey> secretKeys);
}
