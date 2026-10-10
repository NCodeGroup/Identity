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

using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Logic;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.DeviceAuthorization.Logic;

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
