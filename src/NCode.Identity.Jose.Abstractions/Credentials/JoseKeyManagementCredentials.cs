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

using JetBrains.Annotations;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Credentials;

/// <summary>
/// Contains the per-recipient cryptographic credentials that are used by <see cref="IJoseSerializer"/> when
/// encrypting a JWE token using the JSON serialization. The content-encryption (<c>enc</c>) and compression
/// (<c>zip</c>) algorithms are shared across all recipients and are therefore specified separately.
/// </summary>
/// <param name="SecretKey">The Key Encryption Key (KEK) to use for this recipient.</param>
/// <param name="KeyManagementAlgorithm">The <see cref="KeyManagementAlgorithm"/> to use for this recipient.</param>
[PublicAPI]
public record JoseKeyManagementCredentials(
    SecretKey SecretKey,
    KeyManagementAlgorithm KeyManagementAlgorithm
) : JoseCredentials(SecretKey);
