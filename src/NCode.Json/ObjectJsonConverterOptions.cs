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

using JetBrains.Annotations;

namespace NCode.Json;

/// <summary>
/// Controls how <see cref="ObjectJsonConverter"/> maps JSON scalar values to CLR types when deserializing into a
/// loosely-typed object graph. The defaults are intentionally lenient — numbers use the narrowest lossless type, and a
/// JSON string is coerced to the most specific matching type (date, <see cref="System.Guid"/>, or number) before
/// falling back to <see cref="string"/> — which is richer than the built-in <see cref="System.Text.Json"/> behavior.
/// Each coercion can be disabled for strict round-trip fidelity.
/// </summary>
[PublicAPI]
public sealed class ObjectJsonConverterOptions
{
    /// <summary>
    /// Gets the default options: <see cref="ObjectJsonIntegerHandling.MostSpecific"/> integers,
    /// <see cref="ObjectJsonRealHandling.Double"/> reals, <see cref="AllowBigInteger"/> enabled, date-like strings
    /// parsed as <see cref="System.DateTimeOffset"/>, and <see cref="System.Guid"/>- and number-like strings coerced to
    /// their respective types.
    /// </summary>
    public static ObjectJsonConverterOptions Default { get; } = new();

    /// <summary>
    /// Gets options that closely approximate the loosely-typed deserialization behavior of Newtonsoft.Json: integers
    /// widen to <see cref="long"/>, reals become <see cref="double"/>, oversized integers become
    /// <see cref="System.Numerics.BigInteger"/>, and date-like strings become <see cref="System.DateTime"/> — but, like
    /// Newtonsoft.Json, number- and <see cref="System.Guid"/>-like strings are left as <see cref="string"/>.
    /// </summary>
    public static ObjectJsonConverterOptions NewtonsoftCompatible { get; } =
        new()
        {
            Integer = ObjectJsonIntegerHandling.Int64,
            Real = ObjectJsonRealHandling.Double,
            AllowBigInteger = true,
            DateString = ObjectJsonDateStringHandling.DateTime,
            CoerceStringToGuid = false,
            CoerceStringToNumber = false,
        };

    /// <summary>
    /// Gets how a JSON integer number is mapped to a CLR type. The default is
    /// <see cref="ObjectJsonIntegerHandling.MostSpecific"/>.
    /// </summary>
    public ObjectJsonIntegerHandling Integer { get; init; } =
        ObjectJsonIntegerHandling.MostSpecific;

    /// <summary>
    /// Gets how a JSON real number is mapped to a CLR type. The default is
    /// <see cref="ObjectJsonRealHandling.Double"/>.
    /// </summary>
    public ObjectJsonRealHandling Real { get; init; } = ObjectJsonRealHandling.Double;

    /// <summary>
    /// Gets a value indicating whether a JSON integer that exceeds the range of <see cref="ulong"/> is read as a
    /// lossless <see cref="System.Numerics.BigInteger"/>. When <see langword="false"/>, such a value falls back to a
    /// (lossy) <see cref="double"/>. The default is <see langword="true"/>.
    /// </summary>
    public bool AllowBigInteger { get; init; } = true;

    /// <summary>
    /// Gets whether a JSON string is interpreted as a date/time. The default is
    /// <see cref="ObjectJsonDateStringHandling.DateTimeOffset"/>; set to <see cref="ObjectJsonDateStringHandling.None"/>
    /// to keep date-like strings as <see cref="string"/>.
    /// </summary>
    public ObjectJsonDateStringHandling DateString { get; init; } =
        ObjectJsonDateStringHandling.DateTimeOffset;

    /// <summary>
    /// Gets a value indicating whether a JSON string that parses as a <see cref="System.Guid"/> is read as a
    /// <see cref="System.Guid"/>. When <see langword="false"/>, such a string stays a <see cref="string"/>. The default
    /// is <see langword="true"/>.
    /// </summary>
    public bool CoerceStringToGuid { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether a JSON string that parses as a number is read as the matching numeric type
    /// (honoring <see cref="Integer"/>, <see cref="Real"/>, and <see cref="AllowBigInteger"/>). When
    /// <see langword="false"/>, such a string stays a <see cref="string"/> for strict round-trip fidelity. The default
    /// is <see langword="true"/>.
    /// </summary>
    public bool CoerceStringToNumber { get; init; } = true;
}
