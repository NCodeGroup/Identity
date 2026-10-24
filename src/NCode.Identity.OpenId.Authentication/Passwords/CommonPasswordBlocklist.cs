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

using System.Collections.Frozen;
using System.Text;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Refuses a password that is on the bundled list of 46,483 common passwords of at least eight characters. The
/// password is compared whole and ignoring case. The list is loaded once, on first use.
/// </summary>
// See ADR-0236.
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
        using var reader = new StreamReader(stream, Encoding.UTF8);

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
