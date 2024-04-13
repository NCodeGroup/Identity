﻿#region Copyright Preamble

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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NCode.Jose.Buffers;

internal class SecureMemory<T> : IMemoryOwner<T>
{
    private int _disposed;
    private SecureMemoryPool<T>? Pool { get; }

    /// <inheritdoc />
    public Memory<T> Memory { get; }

    internal SecureMemory(SecureMemoryPool<T>? pool, int byteCount)
    {
        Pool = pool;

        // https://devblogs.microsoft.com/dotnet/internals-of-the-poh/
        var byteArray = GC.AllocateUninitializedArray<T>(byteCount, pinned: true);
        Memory = MemoryMarshal.CreateFromPinnedArray(byteArray, 0, byteCount);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        SecureZero(Memory.Span);

        Pool?.Return(this);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void SecureZero(Span<T> buffer)
    {
        // NoOptimize to prevent the optimizer from deciding this call is unnecessary
        // NoInlining to prevent the inliner from forgetting that the method was no-optimize
        buffer.Clear();
    }
}