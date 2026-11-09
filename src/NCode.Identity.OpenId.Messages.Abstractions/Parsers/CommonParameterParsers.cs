// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Contains constants for various <see cref="IParameterParser{T}"/> implementations that are used by <c>OAuth</c>
/// and <c>OpenID Connect</c> messages.
/// </summary>
[PublicAPI]
public static class CommonParameterParsers
{
    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="string"/> values.
    /// </summary>
    public static StringParser String { get; } = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse string collections which are separated by the
    /// space ' ' character.
    /// </summary>
    public static StringSetParser StringSet { get; } = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse string collections which are separated by the
    /// space ' ' character.
    /// </summary>
    public static StringListParser StringList { get; } = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="StringValues"/> values.
    /// </summary>
    public static StringValuesParser StringValues { get; } = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="TimeSpan"/> values.
    /// </summary>
    public static TimeSpanParser TimeSpan { get; } = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="Uri"/> values.
    /// </summary>
    public static UriParser Uri { get; } = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="bool"/> values.
    /// </summary>
    public static BoolParser Bool { get; } = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="DateTimeOffset"/> values.
    /// </summary>
    public static DateTimeOffsetParser DateTimeOffset { get; } = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that parses a nullable <see cref="TimeSpan"/> carried as an integer
    /// number of seconds (a JSON number — for example <c>expires_in</c> per OAuth 2.0 §5.1, or <c>interval</c>).
    /// </summary>
    public static DurationSecondsParser DurationSeconds { get; } = new();
}
