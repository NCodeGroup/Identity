// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Json;

/// <summary>
/// Specifies how a JSON integer number is mapped to a CLR type when <see cref="ObjectJsonConverter"/> deserializes
/// into a loosely-typed object graph.
/// </summary>
[PublicAPI]
public enum ObjectJsonIntegerHandling
{
    /// <summary>
    /// Selects the narrowest CLR integer type that losslessly holds the value, trying <see cref="int"/>, then
    /// <see cref="long"/>, then <see cref="ulong"/>.
    /// </summary>
    MostSpecific = 0,

    /// <summary>
    /// Always widens a JSON integer to <see cref="long"/> — falling back to <see cref="ulong"/> for values that exceed
    /// <see cref="long.MaxValue"/> — approximating the behavior of Newtonsoft.Json.
    /// </summary>
    Int64,
}
