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

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace NCode.Identity.Persistence.DataContracts;

/// <summary>
/// Provides the ability to manage a value with a concurrency token.
/// </summary>
/// <param name="Value">The underlying value.</param>
/// <param name="ConcurrencyToken">A value that is used to check for optimistic concurrency violations.</param>
/// <typeparam name="TValue">The type of the underlying value.</typeparam>
[PublicAPI]
public readonly record struct ConcurrentState<TValue>(
    TValue Value,
    [MaxLength(MaxLengths.ConcurrencyToken)]
    string ConcurrencyToken
) : ISupportConcurrencyToken;

/// <summary>
/// Provides a factory for creating instances of <see cref="ConcurrentState{TValue}"/>.
/// </summary>
public static class ConcurrentState
{
    /// <summary>
    /// Creates a new instance of <see cref="ConcurrentState{TValue}"/> with the specified value and concurrency token.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    /// <param name="concurrencyToken">A value that is used to check for optimistic concurrency violations.</param>
    /// <typeparam name="TValue">The type of the underlying value.</typeparam>
    /// <returns>A new instance of <see cref="ConcurrentState{TValue}"/>.</returns>
    public static ConcurrentState<TValue> Create<TValue>(
        TValue value,
        [MaxLength(MaxLengths.ConcurrencyToken)]
        string concurrencyToken)
        => new(value, concurrencyToken);
}
