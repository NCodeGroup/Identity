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
using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography.CryptoProvider;

public abstract class KeyWrapProvider : IDisposable
{
    public SecretKey SecretKey { get; }

    public KeyWrapAlgorithmDescriptor AlgorithmDescriptor { get; }

    protected KeyWrapProvider(SecretKey secretKey, KeyWrapAlgorithmDescriptor descriptor)
    {
        SecretKey = secretKey;
        AlgorithmDescriptor = descriptor;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // nothing
    }

    public abstract ReadOnlySequence<byte> WrapKey(KeyWrapParameters parameters);

    public abstract ReadOnlySequence<byte> UnwrapKey(KeyUnwrapParameters parameters);
}
