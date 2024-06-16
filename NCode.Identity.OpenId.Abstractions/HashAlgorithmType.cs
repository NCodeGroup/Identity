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

namespace NCode.Identity.OpenId;

/// <summary>
/// Specifies which cryptographic hash algorithm to use.
/// </summary>
[PublicAPI]
public enum HashAlgorithmType
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Specifies to use the <c>SHA-1</c> cryptographic hash algorithm.
    /// </summary>
    Sha1 = 20,

    /// <summary>
    /// Specifies to use the <c>SHA-256</c> cryptographic hash algorithm.
    /// </summary>
    Sha256 = 32,

    /// <summary>
    /// Specifies to use the <c>SHA-512</c> cryptographic hash algorithm.
    /// </summary>
    Sha512 = 64
}
