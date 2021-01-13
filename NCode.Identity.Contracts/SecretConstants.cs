#region Copyright Preamble

// 
//    Copyright @ 2021 NCode Group
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

namespace NCode.Identity.Contracts
{
    public static class SecretConstants
    {
        public static class Encodings
        {
            public const string None = "none";
            public const string Base64 = "base64";
            public const string Pem = "pem";
        }

        public static class Algorithms
        {
            public const string None = "none";
            public const string Aes = "aes";
            public const string Rsa = "rsa";
            public const string Dsa = "dsa";
            public const string Ecdsa = "ecdsa";
            public const string Ecdh = "ecdh";
        }

        public static class Types
        {
            public const string SharedSecret = "shared_secret";
            public const string SymmetricKey = "symmetric_key";
            public const string AsymmetricKey = "asymmetric_key";
            public const string Certificate = "certificate";
        }
    }
}