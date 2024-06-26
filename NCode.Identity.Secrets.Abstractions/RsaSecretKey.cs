﻿#region Copyright Preamble

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
using JetBrains.Annotations;

namespace NCode.Identity.Secrets;

/// <summary>
/// Represents an <see cref="AsymmetricSecretKey"/> implementation using <c>RSA</c> cryptographic keys.
/// </summary>
[PublicAPI]
public abstract class RsaSecretKey : AsymmetricSecretKey
{
    /// <summary>
    /// OID for <c>RSA</c> public key cryptography.
    /// </summary>
    public const string Oid = "1.2.840.113549.1.1.1";

    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Rsa;

    /// <summary>
    /// Factory method to create an <see cref="RSA"/> instance from the current <c>RSA</c> key material.
    /// </summary>
    /// <returns>The newly created <see cref="RSA"/> instance</returns>
    public abstract RSA ExportRSA();
}
