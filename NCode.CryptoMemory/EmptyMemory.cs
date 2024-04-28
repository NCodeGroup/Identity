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

using System.Buffers;

namespace NCode.CryptoMemory;

/// <summary>
/// Provides an <see cref="IMemoryOwner{T}"/> implementation that is empty.
/// </summary>
/// <typeparam name="T">The type of elements to store in memory.</typeparam>
public sealed class EmptyMemory<T> : IMemoryOwner<T>
{
    /// <summary>
    /// Gets a singleton instance of <see cref="EmptyMemory{T}"/>.
    /// </summary>
    public static EmptyMemory<T> Singleton { get; } = new();

    /// <inheritdoc />
    public Memory<T> Memory { get; } = Memory<T>.Empty;

    /// <inheritdoc />
    public void Dispose()
    {
        // nothing
    }
}
