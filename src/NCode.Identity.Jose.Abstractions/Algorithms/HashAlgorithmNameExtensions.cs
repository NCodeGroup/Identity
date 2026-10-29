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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides various extension methods for <see cref="HashAlgorithmName"/>.
/// </summary>
/// <remarks>
/// The hash vocabulary is the framework's: every hash algorithm the platform's <see cref="IncrementalHash"/> and
/// <see cref="CryptographicOperations"/> support is available, such as SHA-2 and, where the platform supports it, SHA-3,
/// so a contributed signature algorithm can hash with any of them. <c>MD5</c> is refused by name because its collision
/// resistance is broken (<c>RFC 6151</c>).
/// </remarks>
// See ADR-0190 and ADR-0150.
[PublicAPI]
public static class HashAlgorithmNameExtensions
{
    private static ConcurrentDictionary<string, int> HashSizesBits { get; } =
        new(StringComparer.Ordinal);

    private static ConcurrentDictionary<string, HashFunctionDelegate> HashFunctions { get; } =
        new(StringComparer.Ordinal);

    /// <param name="hashAlgorithmName">Contains the <see cref="HashAlgorithmName"/>.</param>
    extension(HashAlgorithmName hashAlgorithmName)
    {
        /// <summary>
        /// Gets the hash size, in bits, from the specified <see cref="HashAlgorithmName"/>.
        /// </summary>
        /// <returns>The hash size, in bits, from the <see cref="HashAlgorithmName"/>.</returns>
        /// <exception cref="ArgumentException">The specified <see cref="HashAlgorithmName"/> is not supported.</exception>
        public int GetHashSizeBits()
        {
            var name = GetSupportedName(hashAlgorithmName);
            return HashSizesBits.GetOrAdd(name, ComputeHashSizeBits, hashAlgorithmName);
        }

        /// <summary>
        /// Gets a <see cref="HashFunctionDelegate"/> that can be used to hash data using the specified <see cref="HashAlgorithmName"/>.
        /// </summary>
        /// <returns>The <see cref="HashFunctionDelegate"/> that can be used to hash data using the specified <see cref="HashAlgorithmName"/>.</returns>
        /// <exception cref="ArgumentException">The specified <see cref="HashAlgorithmName"/> is not supported.</exception>
        public HashFunctionDelegate GetHashFunction()
        {
            // Resolving the size first refuses an algorithm the platform cannot compute before a delegate is handed out.
            hashAlgorithmName.GetHashSizeBits();

            var name = GetSupportedName(hashAlgorithmName);
            return HashFunctions.GetOrAdd(name, CreateHashFunction, hashAlgorithmName);
        }
    }

    private static string GetSupportedName(HashAlgorithmName hashAlgorithmName)
    {
        var name = hashAlgorithmName.Name;
        if (
            string.IsNullOrEmpty(name)
            || string.Equals(name, HashAlgorithmName.MD5.Name, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw CreateNotSupported(hashAlgorithmName, innerException: null);
        }

        return name;
    }

    private static int ComputeHashSizeBits(string name, HashAlgorithmName hashAlgorithmName)
    {
        try
        {
            using var hash = IncrementalHash.CreateHash(hashAlgorithmName);
            return hash.HashLengthInBytes * 8;
        }
        catch (Exception exception)
            when (exception is CryptographicException or PlatformNotSupportedException)
        {
            throw CreateNotSupported(hashAlgorithmName, exception);
        }
    }

    private static HashFunctionDelegate CreateHashFunction(
        string name,
        HashAlgorithmName hashAlgorithmName
    ) =>
        (ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten) =>
            CryptographicOperations.TryHashData(
                hashAlgorithmName,
                source,
                destination,
                out bytesWritten
            );

    private static ArgumentException CreateNotSupported(
        HashAlgorithmName hashAlgorithmName,
        Exception? innerException
    ) =>
        new(
            $"The {hashAlgorithmName} hash algorithm is not supported.",
            nameof(hashAlgorithmName),
            innerException
        );
}
