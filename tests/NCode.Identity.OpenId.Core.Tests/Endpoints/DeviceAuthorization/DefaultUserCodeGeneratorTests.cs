// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.DeviceAuthorization;

public class DefaultUserCodeGeneratorTests
{
    private const string Alphabet = "BCDFGHJKLMNPQRSTVWXZ";

    private DefaultUserCodeGenerator Generator { get; } = new();

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    public void Generate_WithLength_HasExpectedSignificantCharacterCount(int length)
    {
        var code = Generator.Generate(length);

        var significant = code.Replace("-", string.Empty);
        Assert.Equal(length, significant.Length);
    }

    [Fact]
    public void Generate_WithLength_ContainsOnlyAlphabetAndSeparators()
    {
        var code = Generator.Generate(8);

        foreach (var character in code)
        {
            Assert.True(character == '-' || Alphabet.Contains(character));
        }
    }

    [Fact]
    public void Generate_WithLengthOverGroupSize_InsertsSeparators()
    {
        var code = Generator.Generate(8);

        Assert.Contains('-', code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Generate_WithNonPositiveLength_Throws(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Generator.Generate(length));
    }

    [Fact]
    public void Normalize_LowercaseWithSeparators_ReturnsUppercaseAlphabetOnly()
    {
        var normalized = Generator.Normalize("wdjb-mjht");

        Assert.Equal("WDJBMJHT", normalized);
    }

    [Fact]
    public void Normalize_WithSpacesAndInvalidCharacters_StripsThem()
    {
        var normalized = Generator.Normalize("WD JB@MJ.HT");

        Assert.Equal("WDJBMJHT", normalized);
    }

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        var normalized = Generator.Normalize(string.Empty);

        Assert.Equal(string.Empty, normalized);
    }
}
