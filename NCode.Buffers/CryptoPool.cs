#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

namespace NCode.Buffers;

/// <summary>
/// Provides a resource pool that enables reusing instances of byte arrays that are securely zeroed when returned.
/// </summary>
public static class CryptoPool
{
    /// <summary>
    /// Retrieves a buffer that is at least the requested length.
    /// </summary>
    /// <param name="byteCount">The length of the buffer needed.</param>
    /// <param name="useExactSize"><c>true</c> if the returned <see cref="Memory{T}"/> must be the exact requested size;
    /// otherwise <c>false</c>, if the <see cref="Memory{T}"/> can be the size of the actual lease from the array pool.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the lease.
    /// </returns>
    public static IMemoryOwner<byte> Rent(int byteCount, bool useExactSize = true)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        return new CryptoLease(buffer, useExactSize ? byteCount : buffer.Length);
    }
}
