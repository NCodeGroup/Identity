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

using Moq;
using NIdentity.OpenId.Cryptography.Passwords;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Cryptography.Passwords;

public class PasswordGeneratorTests : BaseTests
{
    private PasswordGeneratorOptions DefaultOptions { get; } = new();
    private Mock<PasswordGenerator> MockPasswordGenerator { get; }
    private PasswordGenerator PasswordGenerator { get; }

    public PasswordGeneratorTests()
    {
        MockPasswordGenerator = CreatePartialMock<PasswordGenerator>(DefaultOptions);
        PasswordGenerator = MockPasswordGenerator.Object;
    }

    [Fact]
    public void Generate_Valid()
    {
        const string password = nameof(password);

        MockPasswordGenerator
            .Setup(_ => _.Generate(DefaultOptions))
            .Returns(password)
            .Verifiable();

        var result = PasswordGenerator.Generate();
        Assert.Equal(password, result);
    }

    [Fact]
    public void Generate_GivenOptions_ThenValid()
    {
        const string password = nameof(password);

        var characterSet = DefaultOptions.CharacterSet;
        var characterSetLength = characterSet.Length;

        MockPasswordGenerator
            .Setup(_ => _.GetRandomInt32(characterSetLength))
            .Returns(0)
            .Verifiable();

        MockPasswordGenerator
            .Setup(_ => _.GenerateCore(DefaultOptions, characterSet))
            .Returns(password)
            .Verifiable();

        MockPasswordGenerator
            .Setup(_ => _.IsValid(DefaultOptions, password))
            .Returns(true)
            .Verifiable();

        var result = PasswordGenerator.Generate(DefaultOptions);
        Assert.Equal(password, result);
    }

    [Fact]
    public void Generate_GivenOptions_WhenTooManyAttempts_ThenThrows()
    {
        const string password = nameof(password);

        DefaultOptions.MaxAttempts = 10;
        var characterSet = DefaultOptions.CharacterSet;
        var characterSetLength = characterSet.Length;

        MockPasswordGenerator
            .Setup(_ => _.GetRandomInt32(characterSetLength))
            .Returns(0)
            .Verifiable();

        MockPasswordGenerator
            .Setup(_ => _.GenerateCore(DefaultOptions, characterSet))
            .Returns(password)
            .Verifiable();

        MockPasswordGenerator
            .Setup(_ => _.IsValid(DefaultOptions, password))
            .Returns(false)
            .Verifiable();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PasswordGenerator.Generate(DefaultOptions)
        );

        Assert.Equal("Too many attempts.", exception.Message);
    }

    [Fact]
    public void GetLength_GivenEqualMinMax_ThenSame()
    {
        const int length = 7;

        var options = new PasswordGeneratorOptions
        {
            MinLength = length,
            MaxLength = length
        };

        var result = PasswordGenerator.GetLength(options);
        Assert.Equal(length, result);
    }

    [Fact]
    public void GetLength_GivenNonEqualMinMax_ThenRandom()
    {
        const int min = 3;
        const int max = 7;
        const int length = 5;

        var options = new PasswordGeneratorOptions
        {
            MinLength = min,
            MaxLength = max
        };

        MockPasswordGenerator
            .Setup(_ => _.GetRandomInt32(min, max + 1))
            .Returns(length)
            .Verifiable();

        var result = PasswordGenerator.GetLength(options);
        Assert.Equal(length, result);
    }

    [Fact]
    public void Enumerate_Valid()
    {
        const int count = 5;
        var passwords = PasswordGenerator.Take(count).ToHashSet();
        Assert.Equal(count, passwords.Count);
    }

    [Fact]
    public void Enumerate_GivenMax_ThenValid()
    {
        const int count = 10;
        DefaultOptions.MaxEnumerations = count;
        var passwords = PasswordGenerator.ToList();
        Assert.Equal(count, passwords.Count);
    }
}
