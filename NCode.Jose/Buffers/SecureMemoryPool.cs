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
using System.Collections.Concurrent;

namespace NCode.Jose.Buffers;

public class SecureMemoryPoolManager
{
    private ConcurrentBag<IDisposable> Pools { get; }
}

/// <summary>
/// Provides a resource pool that enables reusing instances of memory buffers
/// that are pinned during their lifetime and securely zeroed when returned.
/// </summary>
public class SecureMemoryPool<T> : MemoryPool<T>
{
    // most operating systems have a page size of 4096 bytes
    private const int PageSize = 4096;

    /// <summary>
    /// Gets a singleton instance of <see cref="SecureMemoryPool{T}"/>.
    /// </summary>
    public new static MemoryPool<T> Shared { get; } = new SecureMemoryPool<T>();

    private int _disposed;
    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    private ConcurrentQueue<SecureMemory<T>> MemoryQueue { get; } = new();

    /// <inheritdoc />
    public override int MaxBufferSize => Array.MaxLength;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0 || !disposing)
            return;

        MemoryQueue.Clear();
    }

    /// <inheritdoc />
    public override IMemoryOwner<T> Rent(int minByteCount = -1)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (minByteCount <= PageSize)
        {
            return MemoryQueue.TryDequeue(out var memory) ? memory : new SecureMemory<T>(this, PageSize);
        }

        // non-pooled
        return new SecureMemory<T>(null, minByteCount);
    }

    internal void Return(SecureMemory<T> memory)
    {
        if (IsDisposed)
            return;

        MemoryQueue.Enqueue(memory);
    }
}
