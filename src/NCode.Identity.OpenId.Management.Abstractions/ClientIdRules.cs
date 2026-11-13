// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Provides the rule that every requested and stable <c>client_id</c> satisfies, whether the management API or the
/// instance setup registers the client.
/// </summary>
public static class ClientIdRules
{
    /// <summary>
    /// The maximum number of characters in a <c>client_id</c>.
    /// </summary>
    public const int MaxLength = 255;

    /// <summary>
    /// Determines whether a value is a valid <c>client_id</c>: 1 to <see cref="MaxLength"/> characters, each one an
    /// unreserved character of RFC 3986 section 2.3 (<c>A-Z a-z 0-9 - . _ ~</c>).
    /// </summary>
    /// <param name="clientId">The value to check.</param>
    /// <returns><see langword="true"/> when the value is a valid <c>client_id</c>; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid([NotNullWhen(true)] string? clientId)
    {
        if (clientId is null || clientId.Length is 0 or > MaxLength)
        {
            return false;
        }

        foreach (var character in clientId)
        {
            if (!IsUnreservedCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUnreservedCharacter(char character) =>
        character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9')
        || character is '-' or '.' or '_' or '~';
}
