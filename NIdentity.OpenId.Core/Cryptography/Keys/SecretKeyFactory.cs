#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Buffers;
using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.Keys.Material;

namespace NIdentity.OpenId.Cryptography.Keys;

/// <summary>
/// Provides factory methods to create <see cref="SecretKey"/> instances from cryptographic key material.
/// </summary>
public static class SecretKeyFactory
{
    /// <summary>
    /// Defines a callback method to instantiate concrete <see cref="SecretKey"/> instances given cryptographic key material.
    /// </summary>
    /// <typeparam name="T">The concrete <see cref="SecretKey"/> type.</typeparam>
    public delegate T SecretKeyFactoryDelegate<out T>(ReadOnlySpan<byte> keyBytes)
        where T : SecretKey;

    /// <summary>
    /// Factory method to create <see cref="SecretKey"/> instances from cryptographic key material.
    /// </summary>
    /// <param name="keyMaterial">A <see cref="KeyMaterial"/> that contains the cryptographic key material.</param>
    /// <param name="factory">A callback method that instantiates concrete <see cref="SecretKey"/> instances.</param>
    /// <typeparam name="T">The concrete <see cref="SecretKey"/> type.</typeparam>
    /// <returns>The newly created <see cref="SecretKey"/> instance.</returns>
    public static unsafe T Create<T>(KeyMaterial keyMaterial, SecretKeyFactoryDelegate<T> factory)
        where T : SecretKey
    {
        if (keyMaterial is SymmetricKeyMaterial symmetricKeyMaterial)
        {
            return factory(symmetricKeyMaterial.KeyBytes);
        }

        var bufferSize = 4096;
        while (true)
        {
            var lease = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                // ReSharper disable once UnusedVariable
                fixed (byte* pinned = lease)
                {
                    var bytesToZero = bufferSize;
                    try
                    {
                        var buffer = lease.AsSpan(0, bufferSize);
                        if (keyMaterial.TryExportKey(buffer, out var bytesWritten))
                        {
                            bytesToZero = bytesWritten;
                            var pkcs8PrivateKey = buffer[..bytesWritten];
                            return factory(pkcs8PrivateKey);
                        }

                        bufferSize = checked(bufferSize * 2);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(lease.AsSpan(0, bytesToZero));
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(lease);
            }
        }
    }

    /// <summary>
    /// Factory method to create <see cref="AsymmetricSecretKey"/> instances from an <see cref="AsymmetricAlgorithm"/>.
    /// </summary>
    /// <param name="key">The <see cref="AsymmetricAlgorithm"/> that contains cryptographic key material.</param>
    /// <param name="factory">A callback method that instantiates concrete <see cref="AsymmetricSecretKey"/> instances.</param>
    /// <typeparam name="T">The concrete <see cref="AsymmetricSecretKey"/> type.</typeparam>
    /// <returns>The newly created <see cref="AsymmetricSecretKey"/> instance.</returns>
    public static T Create<T>(AsymmetricAlgorithm key, SecretKeyFactoryDelegate<T> factory)
        where T : AsymmetricSecretKey
    {
        var keyMaterial = new AsymmetricKeyMaterial(key, owns: false);
        return Create(keyMaterial, factory);
    }
}
