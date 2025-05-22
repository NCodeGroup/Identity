#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains constants for the maximum lengths of various fields.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SecretMaxLengths
{
    /// <summary>
    /// Specifies the maximum length of a <c>SecretUse</c>.
    /// </summary>
    public const int Use = 100;

    /// <summary>
    /// Specifies the maximum length of a <c>SecretAlgorithm</c>.
    /// </summary>
    public const int Algorithm = 100;

    /// <summary>
    /// Specifies the maximum length of a <c>SecretType</c>.
    /// </summary>
    public const int SecretType = 100;

    /// <summary>
    /// Specifies the maximum length of a <c>EncodingType</c>.
    /// </summary>
    public const int EncodingType = 100;
}
