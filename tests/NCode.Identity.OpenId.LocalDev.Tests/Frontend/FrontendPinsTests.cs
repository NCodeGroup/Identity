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

using NCode.Buffers;
using NCode.Identity.OpenId.LocalDev.Frontend;

namespace NCode.Identity.OpenId.LocalDev.Tests.Frontend;

public class FrontendPinsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        Guid.NewGuid().ToString("N")
    );

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    #region ParseNodeMajor Tests

    [Theory]
    [InlineData("26", 26)]
    [InlineData("v26.1.0", 26)]
    [InlineData(" 26.1.0\n", 26)]
    [InlineData("22.9.0", 22)]
    public void ParseNodeMajor_WhenTextStartsWithMajor_ReturnsMajor(string text, int expected)
    {
        var result = FrontendPins.ParseNodeMajor(text);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void ParseNodeMajor_WhenTextHasNoMajor_ReturnsNull(string text)
    {
        var result = FrontendPins.ParseNodeMajor(text);

        Assert.Null(result);
    }

    #endregion

    #region Parse Tests

    [Theory]
    [InlineData("pnpm@10.34.7")]
    [InlineData("pnpm@10.34.7+sha512.abc")]
    public void Parse_WhenPnpmIsPinned_ReturnsPins(string packageManager)
    {
        var packageJson = $$"""{ "packageManager": "{{packageManager}}" }""";

        var result = FrontendPins.Parse("26\n", packageJson);

        Assert.Equal(new FrontendPins(26, "10.34.7"), result);
    }

    [Fact]
    public void Parse_WhenNodeVersionIsInvalid_Throws()
    {
        const string packageJson = """{ "packageManager": "pnpm@10.34.7" }""";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            FrontendPins.Parse("abc\n", packageJson)
        );

        Assert.Contains("'abc'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenPackageManagerIsMissing_Throws()
    {
        const string packageJson = """{ "name": "frontend" }""";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            FrontendPins.Parse("26", packageJson)
        );

        Assert.Contains("'missing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenPackageManagerIsNotPnpm_Throws()
    {
        const string packageJson = """{ "packageManager": "yarn@4.0.0" }""";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            FrontendPins.Parse("26", packageJson)
        );

        Assert.Contains("'yarn@4.0.0'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Read Tests

    [Fact]
    public void Read_WhenFilesExist_ReturnsPins()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, ".node-version"),
            "v26.1.0\n",
            SecureEncoding.UTF8
        );
        File.WriteAllText(
            Path.Combine(_directory, "package.json"),
            """{ "packageManager": "pnpm@10.34.7+sha512.abc" }""",
            SecureEncoding.UTF8
        );

        var result = FrontendPins.Read(_directory);

        Assert.Equal(new FrontendPins(26, "10.34.7"), result);
    }

    #endregion
}
