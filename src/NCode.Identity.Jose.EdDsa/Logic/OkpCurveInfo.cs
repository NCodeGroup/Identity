#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

namespace NCode.Identity.Jose.EdDsa.Logic;

/// <summary>
/// Immutable description of an OKP (Octet Key Pair) Edwards curve: its JWK <c>crv</c> name, X.509 OID (RFC 8410),
/// and the fixed byte sizes of its seed, public key, and signature.
/// </summary>
internal sealed record OkpCurveInfo(
    string Name,
    string Oid,
    int SeedSize,
    int PublicKeySize,
    int SignatureSize
)
{
    public int KeySizeBits => PublicKeySize * 8;
}
