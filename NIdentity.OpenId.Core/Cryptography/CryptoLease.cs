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
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace NIdentity.OpenId.Cryptography;

internal sealed class CryptoLease : IMemoryOwner<byte>
{
    private byte[] Buffer { get; }
    private GCHandle Handle { get; }
    public Memory<byte> Memory { get; private set; }

    public CryptoLease(byte[] buffer, int byteCount)
    {
        Buffer = buffer;
        Handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        Memory = new Memory<byte>(buffer, 0, byteCount);
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(Memory.Span);
        ArrayPool<byte>.Shared.Return(Buffer);
        Memory = Memory<byte>.Empty;
        Handle.Free();
    }
}
