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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Common abstractions for all secret keys using <see cref="AsymmetricAlgorithm"/>.
/// </summary>
public abstract class AsymmetricSecretKey : SecretKey
{
    /// <summary>
    /// Gets the cryptographic material for the secret key formatted as <c>PKCS#8</c>.
    /// </summary>
    public abstract ReadOnlySpan<byte> Pkcs8PrivateKey { get; }

    /// <summary>
    /// Gets the optional <see cref="X509Certificate2"/> for this secret key.
    /// </summary>
    /// <remarks>
    /// This certificate only contains the public portion.
    /// To create a certificate with both the private and public portions, use <c>CopyWithPrivateKey</c>.
    /// Doing so will create certificates with ephemeral keys and not persist keys to disk.
    /// </remarks>
    public abstract X509Certificate2? Certificate { get; }
}
