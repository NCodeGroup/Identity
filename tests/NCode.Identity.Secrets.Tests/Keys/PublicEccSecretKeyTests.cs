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

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.Keys;

public class PublicEccSecretKeyTests
{
    [Fact]
    public void GetECCurve_WhenSameSizeAsP256_ReturnsActualCurve()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.brainpoolP256r1);
        var key = new PublicEccSecretKey(
            new KeyMetadata { KeyId = "kid" },
            256,
            ecdsa.ExportSubjectPublicKeyInfo(),
            certificateRawData: null
        );

        var curve = key.GetECCurve();

        Assert.True(
            EccCurveSpecification.IsSameNamedCurve(curve, ECCurve.NamedCurves.brainpoolP256r1)
        );
        Assert.False(EccCurveSpecification.IsSameNamedCurve(curve, ECCurve.NamedCurves.nistP256));
    }
}
