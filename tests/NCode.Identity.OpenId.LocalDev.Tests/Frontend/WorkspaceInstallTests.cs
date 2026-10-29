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

public class WorkspaceInstallTests : IDisposable
{
    private const string PnpmVersion = "10.34.7";
    private const string LockText = "lockfileVersion: '9.0'\nimporters:\n  .: {}\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteFile(string relativePath, string text)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, SecureEncoding.UTF8);
    }

    private void CreateWorkspace(
        string modulesText = $$"""{"packageManager":"pnpm@{{PnpmVersion}}"}""",
        string? installedLockText = LockText,
        string? lockText = LockText
    )
    {
        Directory.CreateDirectory(_root);
        WriteFile(Path.Combine("node_modules", ".modules.yaml"), modulesText);

        if (installedLockText is not null)
        {
            WriteFile(Path.Combine("node_modules", ".pnpm", "lock.yaml"), installedLockText);
        }

        if (lockText is not null)
        {
            WriteFile("pnpm-lock.yaml", lockText);
        }
    }

    #region FindProblems Tests

    [Fact]
    public void FindProblems_WhenHealthy_ReturnsEmpty()
    {
        CreateWorkspace();

        var result = WorkspaceInstall.FindProblems(_root, PnpmVersion);

        Assert.Empty(result);
    }

    [Fact]
    public void FindProblems_WhenNodeModulesMissing_ReturnsNotInstalled()
    {
        Directory.CreateDirectory(_root);
        WriteFile("pnpm-lock.yaml", LockText);

        var result = WorkspaceInstall.FindProblems(_root, PnpmVersion);

        var problem = Assert.Single(result);
        Assert.Equal(
            "The pnpm workspace is not installed (src/frontend/node_modules/.modules.yaml is missing).",
            problem
        );
    }

    [Fact]
    public void FindProblems_WhenModulesFileIsGarbage_ReturnsUnreadable()
    {
        CreateWorkspace(modulesText: "not json: [");

        var result = WorkspaceInstall.FindProblems(_root, PnpmVersion);

        var problem = Assert.Single(result);
        Assert.Equal(
            "src/frontend/node_modules/.modules.yaml is unreadable, so the workspace install cannot be verified.",
            problem
        );
    }

    [Fact]
    public void FindProblems_WhenPnpmVersionDiffers_ReturnsPnpmProblem()
    {
        CreateWorkspace(modulesText: """{"packageManager":"pnpm@9.12.0"}""");

        var result = WorkspaceInstall.FindProblems(_root, PnpmVersion);

        var problem = Assert.Single(result);
        Assert.Contains("pnpm@9.12.0", problem, StringComparison.Ordinal);
        Assert.Contains("pnpm@10.34.7", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void FindProblems_WhenPackageManagerMissing_ReturnsUnknownPnpmProblem()
    {
        CreateWorkspace(modulesText: "{}");

        var result = WorkspaceInstall.FindProblems(_root, PnpmVersion);

        var problem = Assert.Single(result);
        Assert.Contains("an unknown pnpm", problem, StringComparison.Ordinal);
        Assert.Contains("pnpm@10.34.7", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void FindProblems_WhenLockfileDiffers_ReturnsLockfileProblem()
    {
        CreateWorkspace(lockText: LockText + "packages:\n  left-pad@1.0.0: {}\n");

        var result = WorkspaceInstall.FindProblems(_root, PnpmVersion);

        var problem = Assert.Single(result);
        Assert.Contains("pnpm-lock.yaml", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void FindProblems_WhenLockfileDiffersOnlyByLineEndings_ReturnsEmpty()
    {
        CreateWorkspace(lockText: LockText.ReplaceLineEndings("\r\n"));

        var result = WorkspaceInstall.FindProblems(_root, PnpmVersion);

        Assert.Empty(result);
    }

    [Fact]
    public void FindProblems_WhenInstalledLockfileMissing_ReturnsLockfileProblem()
    {
        CreateWorkspace(installedLockText: null);

        var result = WorkspaceInstall.FindProblems(_root, PnpmVersion);

        var problem = Assert.Single(result);
        Assert.Contains("pnpm-lock.yaml", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void FindProblems_WhenNoLockfile_ReturnsEmpty()
    {
        CreateWorkspace(installedLockText: null, lockText: null);

        var result = WorkspaceInstall.FindProblems(_root, PnpmVersion);

        Assert.Empty(result);
    }

    [Fact]
    public void FindProblems_WhenPnpmWrongAndLockfileChanged_ReturnsBothProblems()
    {
        CreateWorkspace(
            modulesText: """{"packageManager":"pnpm@9.12.0"}""",
            lockText: LockText + "packages:\n  left-pad@1.0.0: {}\n"
        );

        var result = WorkspaceInstall.FindProblems(_root, PnpmVersion);

        Assert.Equal(2, result.Count);
        Assert.Contains("pnpm@9.12.0", result[0], StringComparison.Ordinal);
        Assert.Contains("pnpm-lock.yaml", result[1], StringComparison.Ordinal);
    }

    #endregion
}
