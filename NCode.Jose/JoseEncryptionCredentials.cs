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

using NCode.Jose.Algorithms.AuthenticatedEncryption;
using NCode.Jose.Algorithms.Compression;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.SecretKeys;

namespace NCode.Jose;

/// <summary>
/// Contains the set of cryptographic credentials that are used by <see cref="IJoseSerializer"/> when encrypting a JWE token.
/// </summary>
/// <param name="SecretKey">The Key Encryption Key (KEK) to use for encryption.</param>
/// <param name="KeyManagementAlgorithm">The <see cref="IKeyManagementAlgorithm"/> to use for key management.</param>
/// <param name="AuthenticatedEncryptionAlgorithm">The <see cref="IAuthenticatedEncryptionAlgorithm"/> to use for encryption.</param>
/// <param name="CompressionAlgorithm">The optional <see cref="ICompressionAlgorithm"/> to use for compression.</param>
public record JoseEncryptionCredentials(
        SecretKey SecretKey,
        IKeyManagementAlgorithm KeyManagementAlgorithm,
        IAuthenticatedEncryptionAlgorithm AuthenticatedEncryptionAlgorithm,
        ICompressionAlgorithm? CompressionAlgorithm = null)
    : JoseEncodeCredentials(SecretKey);
