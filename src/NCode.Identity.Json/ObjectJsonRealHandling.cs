// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Json;

/// <summary>
/// Specifies how a JSON real (fractional or exponential) number is mapped to a CLR type when
/// <see cref="ObjectJsonConverter"/> deserializes into a loosely-typed object graph.
/// </summary>
[PublicAPI]
public enum ObjectJsonRealHandling
{
    /// <summary>
    /// Reads a JSON real number as <see cref="double"/>.
    /// </summary>
    Double = 0,

    /// <summary>
    /// Reads a JSON real number as <see cref="decimal"/>, falling back to <see cref="double"/> when the value lies
    /// outside the range of <see cref="decimal"/>.
    /// </summary>
    Decimal,
}
