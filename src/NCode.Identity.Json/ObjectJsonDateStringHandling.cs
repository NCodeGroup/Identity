// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Json;

/// <summary>
/// Specifies whether <see cref="ObjectJsonConverter"/> attempts to interpret a JSON <em>string</em> as a date/time
/// when deserializing into a loosely-typed object graph. A JSON number is never interpreted as a date; only quoted
/// strings are considered, and only when enabled here.
/// </summary>
[PublicAPI]
public enum ObjectJsonDateStringHandling
{
    /// <summary>
    /// JSON strings are always read as <see cref="string"/>; no date/time parsing is attempted. This is the default
    /// and matches the behavior of <see cref="System.Text.Json"/>.
    /// </summary>
    None = 0,

    /// <summary>
    /// A JSON string that parses as a date/time is read as <see cref="System.DateTime"/>; any other string is read as
    /// <see cref="string"/>. This approximates the default date handling of Newtonsoft.Json.
    /// </summary>
    DateTime,

    /// <summary>
    /// A JSON string that parses as a date/time is read as <see cref="System.DateTimeOffset"/>; any other string is
    /// read as <see cref="string"/>.
    /// </summary>
    DateTimeOffset,
}
