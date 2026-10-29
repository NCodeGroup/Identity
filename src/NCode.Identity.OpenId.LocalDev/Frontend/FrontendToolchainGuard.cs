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

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using NCode.Buffers;

namespace NCode.Identity.OpenId.LocalDev.Frontend;

/// <summary>
/// Fails the AppHost at startup, with one message that names the repair command, when the frontend toolchain is not
/// ready: no Node of the pinned major is available, or the installed pnpm workspace no longer matches the repository.
/// </summary>
/// <param name="frontendDirectory">The directory that contains the frontend workspace.</param>
internal class FrontendToolchainGuard(string frontendDirectory)
{
    /// <summary>
    /// The command that repairs a stale or missing frontend toolchain.
    /// </summary>
    internal const string RepairCommand = "./build/frontend-install.ps1";

    /// <summary>
    /// Prepends a directory to a <c>PATH</c> value unless it is already one of its entries.
    /// </summary>
    /// <param name="path">The current <c>PATH</c> value.</param>
    /// <param name="directory">The directory to prepend.</param>
    /// <returns>The <c>PATH</c> value that starts with <paramref name="directory"/>.</returns>
    internal static string PrependToPath(string path, string directory)
    {
        if (path.Split(Path.PathSeparator).Contains(directory, StringComparer.OrdinalIgnoreCase))
            return path;

        return path.Length == 0 ? directory : $"{directory}{Path.PathSeparator}{path}";
    }

    /// <summary>
    /// Formats the single message that lists every problem and names the repair command.
    /// </summary>
    /// <param name="problems">The problems to list.</param>
    /// <returns>The message.</returns>
    internal static string FormatMessage(IReadOnlyList<string> problems)
    {
        var lines = new List<string>(problems.Count + 2)
        {
            "The frontend toolchain is not ready, so the frontend resources would fail to start:",
        };
        lines.AddRange(problems.Select(problem => $"- {problem}"));
        lines.Add($"Run {RepairCommand} to repair it, then start the AppHost again.");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Gets the NVS <c>node</c> root directory.
    /// </summary>
    /// <returns>
    /// The directory, or <see langword="null"/> when this is not Windows or the directory does not exist.
    /// </returns>
    [ExcludeFromCodeCoverage(Justification = "Ambient environment passthrough.")]
    internal virtual string? GetNvsNodeRoot()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "nvs",
            "node"
        );

        return Directory.Exists(root) ? root : null;
    }

    /// <summary>
    /// Gets the <c>PATH</c> value of this process.
    /// </summary>
    /// <returns>The <c>PATH</c> value, or an empty string when it is not set.</returns>
    [ExcludeFromCodeCoverage(Justification = "Ambient environment passthrough.")]
    internal virtual string GetPath() => Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

    /// <summary>
    /// Sets the <c>PATH</c> value of this process, which child processes inherit.
    /// </summary>
    /// <param name="path">The new <c>PATH</c> value.</param>
    [ExcludeFromCodeCoverage(Justification = "Ambient environment passthrough.")]
    internal virtual void SetPath(string path) => Environment.SetEnvironmentVariable("PATH", path);

    /// <summary>
    /// Runs <c>node --version</c> with the <c>PATH</c> of this process.
    /// </summary>
    /// <returns>The trimmed version text, or <see langword="null"/> when <c>node</c> cannot be run.</returns>
    [ExcludeFromCodeCoverage(Justification = "Ambient environment passthrough.")]
    internal virtual string? ReadNodeVersion()
    {
        var startInfo = new ProcessStartInfo("node", "--version")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = SecureEncoding.UTF8,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            var output = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    output.AppendLine(e.Data);
            };
            process.BeginOutputReadLine();

            if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            // The parameterless overload waits until the redirected output has been fully read.
            process.WaitForExit();

            return process.ExitCode == 0 ? output.ToString().Trim() : null;
        }
        catch (Win32Exception)
        {
            // there is no node on PATH
            return null;
        }
    }

    /// <summary>
    /// Finds every reason why the frontend toolchain is not ready.
    /// </summary>
    /// <param name="pins">The toolchain versions that the frontend workspace pins.</param>
    /// <returns>The human-readable problems, which is empty when the toolchain is ready.</returns>
    internal IReadOnlyList<string> FindProblems(FrontendPins pins)
    {
        var problems = new List<string>();

        var nodeVersion = ReadNodeVersion();
        if (nodeVersion is null)
        {
            problems.Add(
                $"Node.js {pins.NodeMajor} (pinned in src/frontend/.node-version) was not found: NVS has no install of it and 'node' is not on PATH."
            );
        }
        else if (FrontendPins.ParseNodeMajor(nodeVersion) != pins.NodeMajor)
        {
            problems.Add(
                $"Node.js {pins.NodeMajor} is pinned in src/frontend/.node-version, but 'node' on PATH is {nodeVersion}."
            );
        }

        problems.AddRange(WorkspaceInstall.FindProblems(frontendDirectory, pins.PnpmVersion));

        return problems;
    }

    /// <summary>
    /// Puts the newest NVS install of the pinned Node major on the <c>PATH</c> of this process, then verifies that the
    /// toolchain is ready.
    /// </summary>
    /// <exception cref="InvalidOperationException">The toolchain is not ready.</exception>
    internal void EnsureReady()
    {
        var pins = FrontendPins.Read(frontendDirectory);

        var nvsNodeRoot = GetNvsNodeRoot();
        if (nvsNodeRoot is not null)
        {
            var nodeDirectory = NvsNode.FindDirectory(nvsNodeRoot, pins.NodeMajor);
            if (nodeDirectory is not null)
            {
                SetPath(PrependToPath(GetPath(), nodeDirectory));
            }
        }

        var problems = FindProblems(pins);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(FormatMessage(problems));
        }
    }
}
