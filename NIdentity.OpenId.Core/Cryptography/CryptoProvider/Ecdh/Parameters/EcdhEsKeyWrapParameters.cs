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
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Parameters;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Ecdh.Parameters;

public record EcdhEsKeyWrapParameters
(
    ECDiffieHellman RecipientKey,
    int KeySizeBits,
    string PartyUInfo,
    string PartyVInfo
) : KeyWrapParameters, IEcdhEsAgreement;

// TODO

public record EccWrapNewKeyParameters
(
    string PartyUInfo,
    string PartyVInfo
) : WrapNewKeyParameters;

// Ecc
// Wrap: output(epk(kty,x,y,d,crv)), enc/alg, apu, apv
// Unwrap: epk(kty,x,y,crv), enc/alg, apu, apv

// AesGcm
// Wrap: output(iv, tag)
// Unwrap: iv, tag

// Pbse2
// Wrap: alg, output(p2c, p2s)
// Unwrap: alg, p2c, p2s
