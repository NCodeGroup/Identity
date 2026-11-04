// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;

namespace NCode.Identity.Jose.SdJwt;

/// <summary>
/// Builds and validates JSON Pointers (RFC 6901), which name the claims an issuer makes selectively disclosable and the
/// place of each disclosed claim in the resolved claims.
/// </summary>
internal static class JsonPointer
{
    private const char Separator = '/';
    private const char Escape = '~';
    private const char EscapedEscape = '0';
    private const char EscapedSeparator = '1';

    /// <summary>
    /// Appends an object member name to a pointer, escaping <c>~</c> and <c>/</c> as RFC 6901 §3 requires.
    /// </summary>
    /// <param name="pointer">The pointer of the object.</param>
    /// <param name="memberName">The member name.</param>
    /// <returns>The pointer of the member.</returns>
    public static string AppendMember(string pointer, string memberName)
    {
        var builder = new StringBuilder(pointer.Length + 1 + memberName.Length);
        builder.Append(pointer);
        builder.Append(Separator);
        foreach (var c in memberName)
        {
            switch (c)
            {
                case Escape:
                    builder.Append(Escape);
                    builder.Append(EscapedEscape);
                    break;

                case Separator:
                    builder.Append(Escape);
                    builder.Append(EscapedSeparator);
                    break;

                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Appends an array index to a pointer.
    /// </summary>
    /// <param name="pointer">The pointer of the array.</param>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>The pointer of the element.</returns>
    public static string AppendIndex(string pointer, int index) =>
        string.Concat(pointer, "/", index.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Determines whether a value is a well-formed JSON Pointer that names a member or element rather than the whole
    /// document: it starts with <c>/</c>, and every <c>~</c> is followed by <c>0</c> or <c>1</c>.
    /// </summary>
    /// <param name="pointer">The value to check.</param>
    /// <returns><c>true</c> when the value is a well-formed pointer to a member or element; otherwise <c>false</c>.</returns>
    public static bool IsWellFormed(string pointer)
    {
        if (pointer.Length == 0 || pointer[0] != Separator)
            return false;

        for (var i = 0; i < pointer.Length; i++)
        {
            if (pointer[i] != Escape)
                continue;

            if (i + 1 >= pointer.Length)
                return false;

            var next = pointer[i + 1];
            if (next is not (EscapedEscape or EscapedSeparator))
                return false;

            i++;
        }

        return true;
    }

    /// <summary>
    /// Returns the first reference token of a pointer, unescaped: the name of the top-level member it names.
    /// </summary>
    /// <param name="pointer">A well-formed pointer.</param>
    /// <returns>The unescaped first reference token.</returns>
    public static string GetFirstToken(string pointer)
    {
        var end = pointer.IndexOf(Separator, 1);
        var token = end < 0 ? pointer[1..] : pointer[1..end];
        return token
            .Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);
    }
}
