// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.LocalDev.Frontend;

namespace NCode.Identity.OpenId.LocalDev.Tests.Frontend;

public class NvsNodeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateInstall(string version, bool withX64 = true)
    {
        var versionDirectory = Path.Combine(_root, version);
        Directory.CreateDirectory(versionDirectory);

        var x64Directory = Path.Combine(versionDirectory, "x64");
        if (withX64)
        {
            Directory.CreateDirectory(x64Directory);
        }

        return x64Directory;
    }

    #region FindDirectory Tests

    [Fact]
    public void FindDirectory_WhenMultipleVersions_PicksNumericallyNewest()
    {
        CreateInstall("22.9.0");
        var expected = CreateInstall("22.22.2");

        var result = NvsNode.FindDirectory(_root, 22);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FindDirectory_WhenOtherMajorIsNewer_IgnoresOtherMajor()
    {
        var expected = CreateInstall("22.9.0");
        CreateInstall("24.1.0");

        var result = NvsNode.FindDirectory(_root, 22);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FindDirectory_WhenDirectoryIsNotAVersion_IgnoresDirectory()
    {
        var expected = CreateInstall("22.9.0");
        CreateInstall("22.foo");

        var result = NvsNode.FindDirectory(_root, 22);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FindDirectory_WhenNewestHasNoX64_ReturnsNextNewest()
    {
        var expected = CreateInstall("22.9.0");
        CreateInstall("22.22.2", withX64: false);

        var result = NvsNode.FindDirectory(_root, 22);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FindDirectory_WhenNoInstallOfMajor_ReturnsNull()
    {
        CreateInstall("24.1.0");

        var result = NvsNode.FindDirectory(_root, 22);

        Assert.Null(result);
    }

    [Fact]
    public void FindDirectory_WhenRootDoesNotExist_ReturnsNull()
    {
        var result = NvsNode.FindDirectory(_root, 22);

        Assert.Null(result);
    }

    #endregion
}
