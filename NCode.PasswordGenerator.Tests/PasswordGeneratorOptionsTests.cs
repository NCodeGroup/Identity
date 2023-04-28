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

namespace NCode.PasswordGenerator.Tests;

public class PasswordGeneratorOptionsTests : BaseTests
{
    [Fact]
    public void MinLength_Valid()
    {
        var options = new PasswordGeneratorOptions
        {
            MinLength = 5,
            MaxLength = 10
        };

        options.MinLength = options.MaxLength;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            options.MinLength = options.MaxLength + 1);
    }

    [Fact]
    public void MaxLength_Valid()
    {
        var options = new PasswordGeneratorOptions
        {
            MinLength = 5,
            MaxLength = 10
        };

        options.MaxLength = options.MinLength;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            options.MaxLength = options.MinLength - 1);
    }

    [Fact]
    public void ExactLength_Valid()
    {
        var options = new PasswordGeneratorOptions
        {
            MinLength = 5,
            MaxLength = 10
        };

        options.ExactLength = options.MinLength - 1;

        Assert.Equal(4, options.MinLength);
        Assert.Equal(4, options.MaxLength);

        options.ExactLength = options.MaxLength + 1;

        Assert.Equal(5, options.MinLength);
        Assert.Equal(5, options.MaxLength);
    }

    [Fact]
    public void SetLengthRange_Valid()
    {
        var options = new PasswordGeneratorOptions
        {
            MinLength = 5,
            MaxLength = 10
        };

        options.SetLengthRange(1, 3);

        Assert.Equal(1, options.MinLength);
        Assert.Equal(3, options.MaxLength);

        options.SetLengthRange(13, 17);

        Assert.Equal(13, options.MinLength);
        Assert.Equal(17, options.MaxLength);
    }

    [Fact]
    public void SetLengthRange_GivenInvalid_ThenThrows()
    {
        var options = new PasswordGeneratorOptions();

        Assert.Throws<InvalidOperationException>(() =>
            options.SetLengthRange(7, 5));
    }

    [Fact]
    public void CharacterSet_Valid()
    {
        var options = new PasswordGeneratorOptions
        {
            MinLowercaseCharacters = 0,
            MinUppercaseCharacters = 0,
            MinNumericCharacters = 0,
            MinSpecialCharacters = 0
        };

        Assert.Empty(options.CharacterSet);

        options.MinLowercaseCharacters = 1;
        Assert.Equal("abcdefghijklmnopqrstuvwxyz", options.CharacterSet);

        options.MinUppercaseCharacters = 1;
        Assert.Equal("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", options.CharacterSet);

        options.MinNumericCharacters = 1;
        Assert.Equal("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", options.CharacterSet);

        options.MinSpecialCharacters = 1;
        Assert.Equal("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!;#$%&()*+,-./:;<=>?@[]^_`{|}~", options.CharacterSet);
    }

    [Theory]
    [InlineData(2, "__", -1)]
    [InlineData(2, "___", 2)]
    [InlineData(2, "012345___", 8)]
    [InlineData(3, "012345aaa____", 12)]
    public void AreIdentical_Valid(int maxConsecutive, string password, int duplicateIndex)
    {
        var options = new PasswordGeneratorOptions
        {
            MaxConsecutiveIdenticalCharacters = maxConsecutive
        };

        Assert.True(duplicateIndex < password.Length);

        for (var i = 0; i < password.Length; ++i)
        {
            var result = options.AreIdentical(i, password);
            Assert.Equal(i == duplicateIndex, result);
        }
    }

    [Theory]
    [MemberData(nameof(IsValidTestData))]
    public void IsValid(PasswordGeneratorOptions options, string password, bool expected)
    {
        var result = options.IsValid(password);
        Assert.Equal(expected, result);
    }

    public static IEnumerable<object[]> IsValidTestData()
    {
        yield return new object[]
        {
            new PasswordGeneratorOptions(),
            string.Empty,
            false
        };

        yield return new object[]
        {
            new PasswordGeneratorOptions { MinLength = 1, MaxLength = 7 },
            new string('a', 8),
            false
        };

        yield return new object[]
        {
            new PasswordGeneratorOptions { MinLength = 1, MaxLength = 10 },
            "aB1%",
            true
        };

        yield return new object[]
        {
            new PasswordGeneratorOptions { MinLength = 1, MaxLength = 10, MinLowercaseCharacters = 0 },
            "B1%",
            true
        };

        yield return new object[]
        {
            new PasswordGeneratorOptions { MinLength = 1, MaxLength = 10, MinUppercaseCharacters = 0 },
            "a1%",
            true
        };

        yield return new object[]
        {
            new PasswordGeneratorOptions { MinLength = 1, MaxLength = 10, MinNumericCharacters = 0 },
            "aB%",
            true
        };

        yield return new object[]
        {
            new PasswordGeneratorOptions { MinLength = 1, MaxLength = 10, MinSpecialCharacters = 0 },
            "aB1",
            true
        };

        yield return new object[]
        {
            new PasswordGeneratorOptions { MinLength = 1, MaxLength = 10, SpecialCharacters = "^" },
            "aB1%",
            false
        };

        yield return new object[]
        {
            new PasswordGeneratorOptions { MinLength = 1, MaxLength = 10, SpecialCharacters = "^" },
            "aB1^",
            true
        };

        yield return new object[]
        {
            new PasswordGeneratorOptions
            {
                MinLength = 1,
                MaxLength = 10,
                MinUppercaseCharacters = 0,
                MinNumericCharacters = 0,
                MinSpecialCharacters = 0,
                MaxConsecutiveIdenticalCharacters = 2
            },
            "aa",
            true
        };

        yield return new object[]
        {
            new PasswordGeneratorOptions
            {
                MinLength = 1,
                MaxLength = 10,
                MinUppercaseCharacters = 0,
                MinNumericCharacters = 0,
                MinSpecialCharacters = 0,
                MaxConsecutiveIdenticalCharacters = 2
            },
            "aaa",
            false
        };
    }
}
