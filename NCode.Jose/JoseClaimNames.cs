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

using System.Diagnostics.CodeAnalysis;

namespace NCode.Jose;

[SuppressMessage("ReSharper", "IdentifierTypo")]
public static class JoseClaimNames
{
    public static class Header
    {
        public const string Alg = "alg";
        public const string Enc = "enc";
        public const string Zip = "zip";
        public const string B64 = "b64";

        public const string Iv = "iv";
        public const string Tag = "tag";

        public const string Epk = "epk";
        public const string Kty = "kty";
        public const string Crv = "crv";
        public const string Apu = "apu";
        public const string Apv = "apv";
        public const string X = "x";
        public const string Y = "y";
        public const string D = "d";

        public const string P2c = "p2c";
        public const string P2s = "p2s";

        public const string Kid = "kid";
        public const string Jku = "jku";
        public const string Jwk = "jwk";
        public const string X5u = "x5u";
        public const string X5c = "x5c";
        public const string X5t = "x5t";
        public const string X5tS256 = "x5t#S256";

        public const string Typ = "typ";
        public const string Cty = "cty";
        public const string Crit = "crit";
    }

    public static class Payload
    {
        public const string Iss = "iss";
        public const string Sub = "sub";
        public const string Aud = "aud";
        public const string Exp = "exp";
        public const string Nbf = "nbf";
        public const string Iat = "iat";
        public const string Jti = "jti";

        public const string Actort = "actort";
    }
}
