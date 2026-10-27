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
using NCode.Identity.Jose.EdDsa.KeyTypes;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests;

public class OkpCurvesTests
{
    [Fact]
    public void GetTokenHashFunction_WhenEd25519_ReturnsSha512()
    {
        var hash = OkpCurves.GetTokenHashFunction(OkpCurves.Ed25519, out var size);

        Assert.Equal(64, size);
        var destination = new byte[size];
        Assert.True(hash("abc"u8, destination, out var bytesWritten));
        Assert.Equal(size, bytesWritten);
        Assert.Equal(SHA512.HashData("abc"u8), destination);
    }

    [Fact]
    public void GetTokenHashFunction_WhenEd448_ReturnsShake256With114Bytes()
    {
        Assert.True(Shake256.IsSupported);

        var hash = OkpCurves.GetTokenHashFunction(OkpCurves.Ed448, out var size);

        Assert.Equal(114, size);
        var destination = new byte[size];
        Assert.True(hash("abc"u8, destination, out var bytesWritten));
        Assert.Equal(size, bytesWritten);
        Assert.Equal(Shake256.HashData("abc"u8, 114), destination);
    }
}
