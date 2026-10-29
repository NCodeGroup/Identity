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

using Moq;
using NCode.Buffers;
using NCode.Identity.OpenId.LocalDev.Frontend;

namespace NCode.Identity.OpenId.LocalDev.Tests.Frontend;

public class FrontendToolchainGuardTests : IDisposable
{
    private const string LockText = "lockfileVersion: '9.0'\nimporters:\n  .: {}\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly MockRepository _mocks = new(MockBehavior.Strict);

    public void Dispose()
    {
        _mocks.Verify();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string FrontendDirectory => Path.Combine(_root, "frontend");

    private void WriteFile(string relativePath, string text)
    {
        var path = Path.Combine(FrontendDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, SecureEncoding.UTF8);
    }

    private void CreateFrontend(
        string modulesText = """{"packageManager":"pnpm@10.34.7"}""",
        string lockText = LockText
    )
    {
        WriteFile(".node-version", "26\n");
        WriteFile("package.json", """{"packageManager":"pnpm@10.34.7+sha512.abc"}""");
        WriteFile(Path.Combine("node_modules", ".modules.yaml"), modulesText);
        WriteFile(Path.Combine("node_modules", ".pnpm", "lock.yaml"), LockText);
        WriteFile("pnpm-lock.yaml", lockText);
    }

    private Mock<FrontendToolchainGuard> CreateGuard(
        string? nodeVersion,
        string? nvsNodeRoot = null
    )
    {
        var mock = _mocks.Create<FrontendToolchainGuard>(FrontendDirectory);
        mock.CallBase = true;

        mock.Setup(x => x.GetNvsNodeRoot()).Returns(nvsNodeRoot).Verifiable();
        mock.Setup(x => x.ReadNodeVersion()).Returns(nodeVersion).Verifiable();

        return mock;
    }

    #region PrependToPath Tests

    [Fact]
    public void PrependToPath_WhenDirectoryIsNew_PrependsIt()
    {
        var directory = Path.Combine(_root, "node");
        var path = $"a{Path.PathSeparator}b";

        var result = FrontendToolchainGuard.PrependToPath(path, directory);

        Assert.Equal($"{directory}{Path.PathSeparator}{path}", result);
    }

    [Fact]
    public void PrependToPath_WhenDirectoryIsPresentWithOtherCasing_ReturnsPathUnchanged()
    {
        var directory = Path.Combine(_root, "node");
        var path = $"a{Path.PathSeparator}{directory.ToUpperInvariant()}";

        var result = FrontendToolchainGuard.PrependToPath(path, directory);

        Assert.Equal(path, result);
    }

    [Fact]
    public void PrependToPath_WhenPathIsEmpty_ReturnsDirectory()
    {
        var directory = Path.Combine(_root, "node");

        var result = FrontendToolchainGuard.PrependToPath(string.Empty, directory);

        Assert.Equal(directory, result);
    }

    #endregion

    #region FormatMessage Tests

    [Fact]
    public void FormatMessage_WhenGivenProblems_ListsEachProblemAndNamesTheRepairCommand()
    {
        var result = FrontendToolchainGuard.FormatMessage(["first problem", "second problem"]);

        Assert.Equal(
            "The frontend toolchain is not ready, so the frontend resources would fail to start:\n"
                + "- first problem\n"
                + "- second problem\n"
                + "Run ./build/frontend-install.ps1 to repair it, then start the AppHost again.",
            result
        );
    }

    #endregion

    #region EnsureReady Tests

    [Fact]
    public void EnsureReady_WhenToolchainIsReady_DoesNotThrow()
    {
        CreateFrontend();
        var guard = CreateGuard("v26.1.0");

        guard.Object.EnsureReady();
    }

    [Fact]
    public void EnsureReady_WhenNodeIsMissing_ThrowsWithRepairCommand()
    {
        CreateFrontend();
        var guard = CreateGuard(nodeVersion: null);

        var exception = Assert.Throws<InvalidOperationException>(() => guard.Object.EnsureReady());

        Assert.Contains("Node.js 26", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "./build/frontend-install.ps1",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void EnsureReady_WhenNodeIsAnotherMajor_ThrowsWithActualVersion()
    {
        CreateFrontend();
        var guard = CreateGuard("v24.1.0");

        var exception = Assert.Throws<InvalidOperationException>(() => guard.Object.EnsureReady());

        Assert.Contains("v24.1.0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureReady_WhenPnpmDiffers_ThrowsWithInstalledPnpm()
    {
        CreateFrontend(modulesText: """{"packageManager":"pnpm@9.12.0"}""");
        var guard = CreateGuard("v26.1.0");

        var exception = Assert.Throws<InvalidOperationException>(() => guard.Object.EnsureReady());

        Assert.Contains("pnpm@9.12.0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureReady_WhenLockfileChanged_ThrowsWithLockfileName()
    {
        CreateFrontend(lockText: LockText + "packages:\n  left-pad@1.0.0: {}\n");
        var guard = CreateGuard("v26.1.0");

        var exception = Assert.Throws<InvalidOperationException>(() => guard.Object.EnsureReady());

        Assert.Contains("pnpm-lock.yaml", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureReady_WhenSeveralProblemsExist_ReportsThemInOneMessage()
    {
        CreateFrontend(
            modulesText: """{"packageManager":"pnpm@9.12.0"}""",
            lockText: LockText + "packages:\n  left-pad@1.0.0: {}\n"
        );
        var guard = CreateGuard("v24.1.0");

        var exception = Assert.Throws<InvalidOperationException>(() => guard.Object.EnsureReady());

        Assert.Contains("v24.1.0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("pnpm@9.12.0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("pnpm-lock.yaml changed", exception.Message, StringComparison.Ordinal);
        Assert.Single(
            exception.Message.Split('\n'),
            line => line.StartsWith("Run ", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void EnsureReady_WhenNvsHasThePinnedMajor_PrependsItToPath()
    {
        CreateFrontend();
        var nvsNodeRoot = Path.Combine(_root, "nvs", "node");
        var nodeDirectory = Path.Combine(nvsNodeRoot, "26.1.0", "x64");
        Directory.CreateDirectory(nodeDirectory);
        const string oldPath = "old-path";
        var guard = CreateGuard("v26.1.0", nvsNodeRoot);

        guard.Setup(x => x.GetPath()).Returns(oldPath).Verifiable();
        guard.Setup(x => x.SetPath($"{nodeDirectory}{Path.PathSeparator}{oldPath}")).Verifiable();

        guard.Object.EnsureReady();
    }

    [Fact]
    public void EnsureReady_WhenNvsHasNoInstallOfThePinnedMajor_DoesNotChangePath()
    {
        CreateFrontend();
        var nvsNodeRoot = Path.Combine(_root, "nvs", "node");
        Directory.CreateDirectory(Path.Combine(nvsNodeRoot, "24.1.0", "x64"));
        var guard = CreateGuard("v26.1.0", nvsNodeRoot);

        guard.Setup(x => x.SetPath(It.IsAny<string>())).Verifiable(Times.Never());

        guard.Object.EnsureReady();
    }

    [Fact]
    public void EnsureReady_WhenThereIsNoNvsRoot_DoesNotChangePath()
    {
        CreateFrontend();
        var guard = CreateGuard("v26.1.0");

        guard.Setup(x => x.SetPath(It.IsAny<string>())).Verifiable(Times.Never());

        guard.Object.EnsureReady();
    }

    #endregion
}
