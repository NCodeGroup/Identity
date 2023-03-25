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
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Cryptography.Ecdsa;

public class EcdsaSecretKey : SecretKey
{
    public ECDsa Key { get; }

    public EcdsaSecretKey(Secret secret, ECDsa key)
        : base(secret)
    {
        Key = key;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Key.Dispose();
        }

        base.Dispose(disposing);
    }
}
