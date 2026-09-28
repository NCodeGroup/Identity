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

using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Endpoints.Jwks.Converters;

public class DefaultEccCurveSpecificationRegistryTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithoutAdditionalSpecifications_ContainsBuiltInCurves()
    {
        var registry = new DefaultEccCurveSpecificationRegistry([]);

        Assert.Equal(3, registry.Specifications.Count);
        Assert.Contains(DefaultEccCurveSpecificationRegistry.P256, registry.Specifications);
        Assert.Contains(DefaultEccCurveSpecificationRegistry.P384, registry.Specifications);
        Assert.Contains(DefaultEccCurveSpecificationRegistry.P521, registry.Specifications);
    }

    [Fact]
    public void Constructor_WithAdditionalSpecification_AddsToSupportedSet()
    {
        var custom = new EccCurveSpecification { CurveName = "P-999", CurveSizeBits = 999 };

        var registry = new DefaultEccCurveSpecificationRegistry([custom]);

        Assert.Equal(4, registry.Specifications.Count);
        Assert.True(registry.TryGetByCurveSizeBits(999, out var found));
        Assert.Equal(custom, found);
    }

    [Fact]
    public void Constructor_WithAdditionalSpecificationOfExistingSize_OverridesBuiltIn()
    {
        var custom = new EccCurveSpecification { CurveName = "custom-256", CurveSizeBits = 256 };

        var registry = new DefaultEccCurveSpecificationRegistry([custom]);

        Assert.Equal(3, registry.Specifications.Count);
        Assert.True(registry.TryGetByCurveSizeBits(256, out var found));
        Assert.Equal("custom-256", found.CurveName);
    }

    #endregion

    #region TryGetByCurveSizeBits Tests

    [Theory]
    [InlineData(256, "P-256")]
    [InlineData(384, "P-384")]
    [InlineData(521, "P-521")]
    public void TryGetByCurveSizeBits_WithSupportedSize_ReturnsSpecification(
        int curveSizeBits,
        string expectedCurveName
    )
    {
        var registry = new DefaultEccCurveSpecificationRegistry([]);

        var result = registry.TryGetByCurveSizeBits(curveSizeBits, out var specification);

        Assert.True(result);
        Assert.NotNull(specification);
        Assert.Equal(expectedCurveName, specification.CurveName);
        Assert.Equal(curveSizeBits, specification.CurveSizeBits);
    }

    [Fact]
    public void TryGetByCurveSizeBits_WithUnsupportedSize_ReturnsFalse()
    {
        var registry = new DefaultEccCurveSpecificationRegistry([]);

        var result = registry.TryGetByCurveSizeBits(255, out var specification);

        Assert.False(result);
        Assert.Null(specification);
    }

    #endregion

    #region Built-In Specification Tests

    [Fact]
    public void BuiltInSpecifications_HaveExpectedValues()
    {
        Assert.Equal("P-256", DefaultEccCurveSpecificationRegistry.P256.CurveName);
        Assert.Equal(256, DefaultEccCurveSpecificationRegistry.P256.CurveSizeBits);

        Assert.Equal("P-384", DefaultEccCurveSpecificationRegistry.P384.CurveName);
        Assert.Equal(384, DefaultEccCurveSpecificationRegistry.P384.CurveSizeBits);

        Assert.Equal("P-521", DefaultEccCurveSpecificationRegistry.P521.CurveName);
        Assert.Equal(521, DefaultEccCurveSpecificationRegistry.P521.CurveSizeBits);
    }

    #endregion
}
