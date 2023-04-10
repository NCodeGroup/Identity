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
using System.Diagnostics;
using System.Security.Cryptography;

namespace NIdentity.OpenId.Cryptography.Keys;

internal enum SecretKeyEncoding
{
    Unknown = 0,
    Pem,
    Pkcs8
}

internal readonly ref partial struct SecretKeyReader
{
    private delegate T AsymmetricSecretKeyFactoryDelegate<out T>(ReadOnlySpan<byte> pkcs8PrivateKey)
        where T : AsymmetricSecretKey;

    private ReadOnlySpan<byte> RawData { get; }

    public SecretKeyReader(ReadOnlySpan<byte> rawData)
    {
        RawData = rawData;
    }

    private static unsafe T CreateAsymmetricSecretKey<T>(AsymmetricAlgorithm key, AsymmetricSecretKeyFactoryDelegate<T> factory)
        where T : AsymmetricSecretKey
    {
        var bufferSize = 4096;
        while (true)
        {
            var lease = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                // ReSharper disable once UnusedVariable
                fixed (byte* pinned = lease)
                {
                    var bytesToZero = bufferSize;
                    try
                    {
                        var buffer = lease.AsSpan(0, bufferSize);
                        if (key.TryExportPkcs8PrivateKey(buffer, out var bytesWritten))
                        {
                            bytesToZero = bytesWritten;
                            var pkcs8PrivateKey = buffer[..bytesWritten];
                            return factory(pkcs8PrivateKey);
                        }

                        bufferSize = checked(bufferSize * 2);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(lease.AsSpan(0, bytesToZero));
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(lease);
            }
        }
    }

    private T ReadAsymmetricKey<T>(Func<T> factory, SecretKeyEncoding encoding, ImportPemDelegate<T> importPem)
        where T : AsymmetricAlgorithm =>
        encoding switch
        {
            SecretKeyEncoding.Unknown => throw new InvalidOperationException(),
            SecretKeyEncoding.Pem => ReadPem(importPem),
            SecretKeyEncoding.Pkcs8 => ReadPkcs8(factory),
            _ => throw new InvalidOperationException()
        };

    private T ReadPkcs8<T>(Func<T> factory)
        where T : AsymmetricAlgorithm
    {
        var key = factory();
        try
        {
            key.ImportPkcs8PrivateKey(RawData, out var bytesRead);
            Debug.Assert(bytesRead == RawData.Length);
        }
        catch
        {
            key.Dispose();
            throw;
        }

        return key;
    }
}
