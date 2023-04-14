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
using System.Security.Cryptography;
using System.Text;
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.CryptoProvider.Aes;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Parameters;
using NIdentity.OpenId.Cryptography.CryptoProvider.Pbes2.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Pbes2.Parameters;
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Pbes2;

/// <summary>
/// Provides an implementation of <see cref="KeyWrapProvider"/> using the <c>PBES2</c> key wrap algorithm.
/// https://datatracker.ietf.org/doc/html/rfc2898#section-6.2
/// </summary>
public class Pbes2KeyWrapProvider : KeyWrapProvider
{
    private IAesKeyWrap AesKeyWrap { get; }
    private SharedSecretKey SharedSecretKey { get; }
    private Pbes2KeyWrapAlgorithmDescriptor Descriptor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pbes2KeyWrapProvider"/> class.
    /// </summary>
    /// <param name="aesKeyWrap">Contains the <see cref="IAesKeyWrap"/> instance that implements the <c>AES</c> key wrap algorithm.</param>
    /// <param name="secretKey">Contains the <c>key encryption key (kek)</c> for the <c>PBES2</c> key wrap algorithm.</param>
    /// <param name="descriptor">Contains the <see cref="Pbes2KeyWrapAlgorithmDescriptor"/> the describes the <c>PBES2</c> key wrap algorithm.</param>
    public Pbes2KeyWrapProvider(IAesKeyWrap aesKeyWrap, SharedSecretKey secretKey, Pbes2KeyWrapAlgorithmDescriptor descriptor)
        : base(secretKey, descriptor)
    {
        AesKeyWrap = aesKeyWrap;
        SharedSecretKey = secretKey;
        Descriptor = descriptor;
    }

    /// <inheritdoc />
    public override ReadOnlySequence<byte> WrapKey(KeyWrapParameters parameters)
    {
        if (parameters is not Pbes2KeyWrapParameters typedParameters)
        {
            throw new InvalidOperationException();
        }

        var iterationCount = typedParameters.IterationCount ?? Descriptor.DefaultIterationCount;

        var keyByteCount = Descriptor.KeySizeBytes;
        var keyEncryptionKey = keyByteCount <= BinaryUtility.StackAllocMax ?
            stackalloc byte[keyByteCount] :
            GC.AllocateUninitializedArray<byte>(keyByteCount);

        var algByteCount = Encoding.UTF8.GetByteCount(Descriptor.AlgorithmCode);
        var saltByteCount = algByteCount + 1 + typedParameters.Salt.Length;

        var salt = saltByteCount <= BinaryUtility.StackAllocMax ?
            stackalloc byte[saltByteCount] :
            GC.AllocateUninitializedArray<byte>(saltByteCount);

        Encoding.UTF8.GetBytes(Descriptor.AlgorithmCode, salt);
        salt[algByteCount] = 0;
        typedParameters.Salt.Span.CopyTo(salt[(algByteCount + 1)..]);

        try
        {
            Rfc2898DeriveBytes.Pbkdf2(
                SharedSecretKey.KeyBytes,
                salt,
                keyEncryptionKey,
                iterationCount,
                Descriptor.HashAlgorithmName);

            return AesKeyWrap.WrapKey(keyEncryptionKey, typedParameters.ContentKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyEncryptionKey);
        }
    }

    /// <inheritdoc />
    public override ReadOnlySequence<byte> UnwrapKey(KeyUnwrapParameters parameters)
    {
        if (parameters is not Pbes2KeyUnwrapParameters typedParameters)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        var iterationCount = typedParameters.IterationCount ?? Descriptor.DefaultIterationCount;

        var keyByteCount = Descriptor.KeySizeBytes;
        var keyEncryptionKey = keyByteCount <= BinaryUtility.StackAllocMax ?
            stackalloc byte[keyByteCount] :
            GC.AllocateUninitializedArray<byte>(keyByteCount, pinned: true);

        var algByteCount = Encoding.UTF8.GetByteCount(Descriptor.AlgorithmCode);
        var saltByteCount = algByteCount + 1 + typedParameters.Salt.Length;

        var salt = saltByteCount <= BinaryUtility.StackAllocMax ?
            stackalloc byte[saltByteCount] :
            GC.AllocateUninitializedArray<byte>(saltByteCount);

        Encoding.UTF8.GetBytes(Descriptor.AlgorithmCode, salt);
        salt[algByteCount] = 0;
        typedParameters.Salt.Span.CopyTo(salt[(algByteCount + 1)..]);

        try
        {
            Rfc2898DeriveBytes.Pbkdf2(
                SharedSecretKey.KeyBytes,
                salt,
                keyEncryptionKey,
                iterationCount,
                Descriptor.HashAlgorithmName);

            return AesKeyWrap.UnwrapKey(keyEncryptionKey, typedParameters.EncryptedContentKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyEncryptionKey);
        }
    }
}
