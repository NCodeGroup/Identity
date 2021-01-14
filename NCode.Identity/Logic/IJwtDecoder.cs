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

/*
    JSON Web Signature (JWS)
    https://tools.ietf.org/html/rfc7515

    JSON Web Encryption (JWE)
    https://tools.ietf.org/html/rfc7516

    JSON Web Key (JWK)
    https://tools.ietf.org/html/rfc7517

    JSON Web Algorithms (JWA)
    https://tools.ietf.org/html/rfc7518

    JSON Web Token (JWT)
    https://tools.ietf.org/html/rfc7519

    Examples of Protecting Content Using JSON Object Signing and Encryption(JOSE)
    https://tools.ietf.org/html/rfc7520

    JSON Web Key (JWK)
    https://tools.ietf.org/html/rfc7517

    JSON Web Algorithms (JWA)
    https://tools.ietf.org/html/rfc7518

    Cross-Platform Cryptography in .NET Core and .NET 5
    https://docs.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography

    SymmetricAlgorithm

        BYTE ARRAY KEY (BASE64) -> Convert.FromBase64()

    AsymmetricAlgorithm

        RSA KEY (PEM) -> RSA.ImportFromPem()
        ECDH KEY (PEM) -> ECDiffieHellman.ImportFromPem()
        ECDSA KEY (PEM) -> ECDsa.ImportFromPem()

    Certificates

        RSA KEY PAIR (PEM) -> X509Certificate2.ImportFromPem()
        ECDsa KEY PAIR (PEM) -> X509Certificate2.ImportFromPem()
        ECDiffieHellman KEY PAIR (PEM) -> X509Certificate2.ImportFromPem()

    Encoding: None, Base64, PEM
    Algorithm: None, AES, RSA, DSA, ECDsa, ECDH
    Type: SymmetricKey, AsymmetricKey, Certificate

    ECDH: Key Exchange
    ECDSA: Signing Only

    Key Encryption
        RSA1_5: X509Certificate2 (aka RSA)
        RSA-OAEP: X509Certificate2 (aka RSA)
        RSA-OAEP-256: X509Certificate2 (aka RSA)
        A128KW: byte[] (aka AES)
        A192KW: byte[] (aka AES)
        A256KW: byte[] (aka AES)
        ECDH-ES: CngKey via EccKey.New(x, y, d, usage: CngKeyUsages.KeyAgreement)
        ECDH-ES+A128KW: CngKey via EccKey.New(x, y, d, CngKeyUsages.KeyAgreement)
        ECDH-ES+A192KW: CngKey via EccKey.New(x, y, d, CngKeyUsages.KeyAgreement)
        ECDH-ES+A256KW: CngKey via EccKey.New(x, y, d, CngKeyUsages.KeyAgreement)
        PBES2-HS256+A128KW: string passphrase
        PBES2-HS384+A192KW: string passphrase
        PBES2-HS512+A256KW: string passphrase
        A128GCMKW: byte[] (aka AES GCM)
        A192GCMKW: byte[] (aka AES GCM)
        A256GCMKW: byte[] (aka AES GCM)

    Content Encryption
        A128CBC-HS256
        A192CBC-HS384
        A256CBC-HS512
        A128GCM
        A192GCM
        A256GCM

    Signing
        Asymmetric: PrivateKey for Encoding
        Asymmetric: PublicKey for Decoding

        none: null
        HS256: byte[]
        HS384: byte[]
        HS512: byte[]
        RS256: X509Certificate2.GetRSAPrivateKey()
        RS384: X509Certificate2.GetRSAPrivateKey()
        RS512: X509Certificate2.GetRSAPrivateKey()
        ES256: CngKey via EccKey.New(x, y, d, CngKeyUsages.Signing) or ECDsa via X509Certificate2.GetECDsaPublicKey()
        ES384: CngKey via EccKey.New(x, y, d, CngKeyUsages.Signing) or ECDsa via X509Certificate2.GetECDsaPublicKey()
        ES512: CngKey via EccKey.New(x, y, d, CngKeyUsages.Signing) or ECDsa via X509Certificate2.GetECDsaPublicKey()
        PS256: X509Certificate2.GetRSAPublicKey()
        PS384: X509Certificate2.GetRSAPublicKey()
        PS512: X509Certificate2.GetRSAPublicKey()
*/
