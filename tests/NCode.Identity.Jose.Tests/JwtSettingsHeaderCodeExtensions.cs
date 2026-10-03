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

using Jose;

namespace NCode.Identity.Jose.Tests;

// jose-jwt 5.3.0 removed the JwtSettings enum-to-header-code lookups (JwsHeaderValue, JwaHeaderValue,
// JweHeaderValue, CompressionHeader). These tests use jose-jwt as the control oracle for the RFC 7518
// header codes, so the removed lookups are reproduced here from the standardized code values.
internal static class JwtSettingsHeaderCodeExtensions
{
    extension(JwtSettings settings)
    {
        public string JwsHeaderValue(JwsAlgorithm algorithm) =>
            algorithm switch
            {
                JwsAlgorithm.none => "none",
                JwsAlgorithm.HS256 => "HS256",
                JwsAlgorithm.HS384 => "HS384",
                JwsAlgorithm.HS512 => "HS512",
                JwsAlgorithm.RS256 => "RS256",
                JwsAlgorithm.RS384 => "RS384",
                JwsAlgorithm.RS512 => "RS512",
                JwsAlgorithm.PS256 => "PS256",
                JwsAlgorithm.PS384 => "PS384",
                JwsAlgorithm.PS512 => "PS512",
                JwsAlgorithm.ES256 => "ES256",
                JwsAlgorithm.ES384 => "ES384",
                JwsAlgorithm.ES512 => "ES512",
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null),
            };

        public string JwaHeaderValue(JweAlgorithm algorithm) =>
            algorithm switch
            {
                JweAlgorithm.RSA1_5 => "RSA1_5",
                JweAlgorithm.RSA_OAEP => "RSA-OAEP",
                JweAlgorithm.RSA_OAEP_256 => "RSA-OAEP-256",
                JweAlgorithm.RSA_OAEP_384 => "RSA-OAEP-384",
                JweAlgorithm.RSA_OAEP_512 => "RSA-OAEP-512",
                JweAlgorithm.DIR => "dir",
                JweAlgorithm.A128KW => "A128KW",
                JweAlgorithm.A192KW => "A192KW",
                JweAlgorithm.A256KW => "A256KW",
                JweAlgorithm.ECDH_ES => "ECDH-ES",
                JweAlgorithm.ECDH_ES_A128KW => "ECDH-ES+A128KW",
                JweAlgorithm.ECDH_ES_A192KW => "ECDH-ES+A192KW",
                JweAlgorithm.ECDH_ES_A256KW => "ECDH-ES+A256KW",
                JweAlgorithm.PBES2_HS256_A128KW => "PBES2-HS256+A128KW",
                JweAlgorithm.PBES2_HS384_A192KW => "PBES2-HS384+A192KW",
                JweAlgorithm.PBES2_HS512_A256KW => "PBES2-HS512+A256KW",
                JweAlgorithm.A128GCMKW => "A128GCMKW",
                JweAlgorithm.A192GCMKW => "A192GCMKW",
                JweAlgorithm.A256GCMKW => "A256GCMKW",
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null),
            };

        public string JweHeaderValue(JweEncryption encryption) =>
            encryption switch
            {
                JweEncryption.A128CBC_HS256 => "A128CBC-HS256",
                JweEncryption.A192CBC_HS384 => "A192CBC-HS384",
                JweEncryption.A256CBC_HS512 => "A256CBC-HS512",
                JweEncryption.A128GCM => "A128GCM",
                JweEncryption.A192GCM => "A192GCM",
                JweEncryption.A256GCM => "A256GCM",
                _ => throw new ArgumentOutOfRangeException(nameof(encryption), encryption, null),
            };

        public string CompressionHeader(JweCompression compression) =>
            compression switch
            {
                JweCompression.DEF => "DEF",
                _ => throw new ArgumentOutOfRangeException(nameof(compression), compression, null),
            };
    }
}
