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
