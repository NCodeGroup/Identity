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
