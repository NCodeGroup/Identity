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
