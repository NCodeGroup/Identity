// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Server;

/// <summary>
/// Provides the command-line handling that the host's commands, such as <c>setup</c> and <c>secret-protection</c>,
/// share.
/// </summary>
internal static class HostCommandLine
{
    /// <summary>
    /// Gets the arguments that the host builds with: none when the first argument is the name of a command, because a
    /// command-line configuration provider would read the command's own arguments as configuration, and the arguments
    /// otherwise.
    /// </summary>
    /// <param name="args">The command-line arguments of the host.</param>
    /// <param name="commandName">The name of the command that the first argument selects.</param>
    /// <returns>The arguments for the host builder.</returns>
    public static string[] GetHostArguments(string[] args, string commandName)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(commandName);

        return args.Length > 0 && string.Equals(args[0], commandName, StringComparison.Ordinal)
            ? []
            : args;
    }
}
