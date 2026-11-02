// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Refuses a password that is on the bundled list of 46,483 common passwords of at least eight characters. The
/// password is compared whole and ignoring case. The list is loaded once, on first use.
/// </summary>
// See ADR-1236.
internal class CommonPasswordBlocklist : IPasswordBlocklist
{
    internal const string ResourceName =
        "NCode.Identity.OpenId.Authentication.Passwords.common-passwords.txt";

    private static Lazy<FrozenSet<string>> Passwords { get; } = new(LoadPasswords);

    /// <inheritdoc />
    public ValueTask<bool> ContainsAsync(
        PasswordPolicyContext context,
        ReadOnlyMemory<char> password,
        CancellationToken cancellationToken
    )
    {
        var passwords = Passwords.Value;
        var lookup = passwords.GetAlternateLookup<ReadOnlySpan<char>>();
        var contains = lookup.Contains(password.Span);
        return ValueTask.FromResult(contains);
    }

    internal static FrozenSet<string> LoadPasswords()
    {
        var assembly = typeof(CommonPasswordBlocklist).Assembly;
        using var stream =
            assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The embedded resource '{ResourceName}' is missing."
            );
        using var reader = new StreamReader(stream, SecureEncoding.UTF8);

        var passwords = new List<string>(46_483);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
                continue;

            passwords.Add(line);
        }

        return passwords.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }
}
