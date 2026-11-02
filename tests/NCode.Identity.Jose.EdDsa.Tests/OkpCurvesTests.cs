// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
