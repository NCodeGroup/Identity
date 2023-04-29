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
using NCode.Buffers;
using NCode.Cryptography.Keys;
using NCode.Jose.KeyManagement;

namespace NCode.Jose.Tests.KeyManagement;

public class EcdhKeyManagementAlgorithmTests : BaseTests
{
    [Fact]
    public void RoundTrip_Valid()
    {
        const string code = nameof(code);
        const string keyId = nameof(keyId);
        const string enc = "dir";
        const string kty = "EC";
        const bool isDirectAgreement = true;
        const int keySizeBytes = 33;

        var curve = ECCurve.NamedCurves.nistP521;
        var algorithm = new EcdhKeyManagementAlgorithm(code, isDirectAgreement);

        var apu = new byte[16];
        RandomNumberGenerator.Fill(apu);

        var apv = new byte[32];
        RandomNumberGenerator.Fill(apv);

        // party 1

        using var key1 = ECDiffieHellman.Create(curve);
        using var secretKey1 = EccSecretKey.Create(keyId, key1);
        var parameters1 = key1.ExportParameters(includePrivateParameters: false);

        var header1 = new Dictionary<string, object>
        {
            ["enc"] = enc,
            ["apu"] = Base64Url.Encode(apu),
            ["apv"] = Base64Url.Encode(apv)
        };

        var cek1 = new byte[keySizeBytes];
        algorithm.NewKey(secretKey1, header1, cek1);

        // party 2

        var epk = (IDictionary<string, object>)header1["epk"];
        var parameters2 = new ECParameters
        {
            Curve = curve,
            Q = new ECPoint
            {
                X = Base64Url.Decode((string)epk["x"]),
                Y = Base64Url.Decode((string)epk["y"]),
            },
            D = Base64Url.Decode((string)epk["d"])
        };
        using var key2 = ECDiffieHellman.Create(parameters2);
        using var secretKey2 = EccSecretKey.Create(keyId, key2);

        var crv = $"P-{secretKey2.KeySizeBits}";
        var header2 = new Dictionary<string, object>
        {
            ["enc"] = enc,
            ["apu"] = Base64Url.Encode(apu),
            ["apv"] = Base64Url.Encode(apv),
            ["epk"] = new Dictionary<string, object>
            {
                ["kty"] = kty,
                ["crv"] = crv,
                ["x"] = Base64Url.Encode(parameters1.Q.X),
                ["y"] = Base64Url.Encode(parameters1.Q.Y)
            }
        };

        var cek2 = new byte[keySizeBytes];
        var result = algorithm.TryUnwrapKey(secretKey2, header2, Array.Empty<byte>(), cek2, out var bytesWritten);

        // assert

        Assert.True(result);
        Assert.Equal(keySizeBytes, bytesWritten);
        Assert.Equal(Convert.ToBase64String(cek1), Convert.ToBase64String(cek2));
    }
}
