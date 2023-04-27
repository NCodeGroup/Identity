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

using NCode.Cryptography.Keys;

namespace NCode.Jose.KeyManagement;

/// <summary>
/// Provides various extensions methods for the <see cref="IKeyManagementAlgorithm"/> interface.
/// </summary>
public static class KeyManagementAlgorithmExtensions
{
    /// <summary>
    /// Performs the cryptographic operation of encrypting a newly generated content encryption key (CEK) with an key encryption key (KEK).
    /// </summary>
    /// <param name="algorithm"></param>
    /// <param name="secretKey">The key encryption key (KEK), for the current cryptographic algorithm.</param>
    /// <param name="header">The JOSE header for the current cryptographic operation.</param>
    /// <param name="contentKey">The destination for the newly generated content encryption key (CEK).</param>
    /// <param name="encryptedContentKey">The destination for the encrypted content encryption key (CEK).</param>
    /// <param name="bytesWritten">When this method returns, contains a value that indicates the number of bytes written to <paramref name="encryptedContentKey"/>.</param>
    /// <returns><c>true</c> if <paramref name="encryptedContentKey"/> is big enough to receive the output; otherwise, <c>false</c>.</returns>
    public static bool TryWrapNewKey(
        this IKeyManagementAlgorithm algorithm,
        SecretKey secretKey,
        IDictionary<string, object> header,
        Span<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        algorithm.NewKey(secretKey, header, contentKey);

        return algorithm.TryWrapKey(
            secretKey,
            header,
            contentKey,
            encryptedContentKey,
            out bytesWritten);
    }
}
