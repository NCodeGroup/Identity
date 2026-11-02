// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using NCode.Buffers;
using NCode.Identity.OpenId.LocalDev.DevelopmentSecrets;

namespace NCode.Identity.OpenId.LocalDev.Tests.DevelopmentSecrets;

public class DevelopmentSecretFileTests : IDisposable
{
    private const string FileName = "secret";

    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string WriteFile(string text)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, FileName);
        File.WriteAllText(path, text, SecureEncoding.UTF8);
        return path;
    }

    #region Ensure Tests

    [Fact]
    public void Ensure_WhenDirectoryIsMissing_CreatesItAndTheFile()
    {
        var directory = Path.Combine(_root, "nested", "LocalDev");

        var path = DevelopmentSecretFile.Ensure(directory, FileName);

        Assert.Equal(Path.Combine(directory, FileName), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Ensure_WhenFileIsMissing_WritesA32ByteBase64UrlValue()
    {
        var path = DevelopmentSecretFile.Ensure(_root, FileName);

        var text = File.ReadAllText(path, SecureEncoding.UTF8);

        Assert.Equal(43, text.Length);
        Assert.Equal(32, Base64Url.DecodeFromChars(text).Length);
    }

    [Fact]
    public void Ensure_WhenFileExists_KeepsItsValue()
    {
        var existingPath = WriteFile("existing-secret");

        var path = DevelopmentSecretFile.Ensure(_root, FileName);

        Assert.Equal(existingPath, path);
        Assert.Equal("existing-secret", File.ReadAllText(path, SecureEncoding.UTF8));
    }

    [Fact]
    public void Ensure_WhenFileIsEmpty_ReplacesIt()
    {
        WriteFile(string.Empty);

        var path = DevelopmentSecretFile.Ensure(_root, FileName);

        var text = File.ReadAllText(path, SecureEncoding.UTF8);
        Assert.Equal(43, text.Length);
    }

    [Fact]
    public void Ensure_WhenCalledTwice_ReturnsTheSameValue()
    {
        var firstPath = DevelopmentSecretFile.Ensure(_root, FileName);
        var firstText = File.ReadAllText(firstPath, SecureEncoding.UTF8);

        var secondPath = DevelopmentSecretFile.Ensure(_root, FileName);
        var secondText = File.ReadAllText(secondPath, SecureEncoding.UTF8);

        Assert.Equal(firstPath, secondPath);
        Assert.Equal(firstText, secondText);
    }

    [Fact]
    public void Ensure_Always_LeavesNoTemporaryFile()
    {
        DevelopmentSecretFile.Ensure(_root, FileName);

        var files = Directory.GetFiles(_root);

        Assert.Single(files);
    }

    #endregion

    #region ReadText Tests

    [Fact]
    public void ReadText_ReturnsTheFileText()
    {
        var path = WriteFile("file-text");

        var result = DevelopmentSecretFile.ReadText(path);

        Assert.Equal("file-text", result);
    }

    #endregion

    #region ReadBase64UrlEncoded Tests

    [Fact]
    public void ReadBase64UrlEncoded_ReturnsTheBase64UrlOfTheFileBytes()
    {
        var path = WriteFile("abc");

        var result = DevelopmentSecretFile.ReadBase64UrlEncoded(path);

        Assert.Equal("YWJj", result);
    }

    #endregion
}
