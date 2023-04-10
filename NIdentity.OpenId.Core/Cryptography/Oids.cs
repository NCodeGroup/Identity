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

namespace NIdentity.OpenId.Cryptography;

public static class Oids
{
    /// <summary>
    /// Digital Signature Algorithm (DSA) subject public key.
    /// </summary>
    public const string Dsa = "1.2.840.10040.4.1";

    /// <summary>
    /// RSAES-PKCS1-v1_5 encryption scheme.
    /// </summary>
    public const string Rsa = "1.2.840.113549.1.1.1";

    /// <summary>
    /// Elliptic curve public key cryptography.
    /// </summary>
    /// <remarks>
    /// Can be used for either <c>ECDsa</c> or <c>ECDiffieHellman</c>.
    /// </remarks>
    public const string Ecc = "1.2.840.10045.2.1";
}
