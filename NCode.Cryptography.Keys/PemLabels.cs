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

namespace NCode.Cryptography.Keys;

internal static class PemLabels
{
    public const string Pkcs8PrivateKey = "PRIVATE KEY";
    public const string EncryptedPkcs8PrivateKey = "ENCRYPTED PRIVATE KEY";
    public const string SpkiPublicKey = "PUBLIC KEY";
    public const string RsaPublicKey = "RSA PUBLIC KEY";
    public const string RsaPrivateKey = "RSA PRIVATE KEY";
    public const string EcPrivateKey = "EC PRIVATE KEY";
}
