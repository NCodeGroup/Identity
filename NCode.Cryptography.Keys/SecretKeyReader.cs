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

using System.Diagnostics;
using System.Security.Cryptography;

namespace NCode.Cryptography.Keys;

/// <summary>
/// Provides the ability to read <see cref="SecretKey"/> instances from binary data.
/// </summary>
public readonly ref partial struct SecretKeyReader
{
    private ReadOnlySpan<byte> Source { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKeyReader"/> struct.
    /// </summary>
    /// <param name="source">The source buffer to read from.</param>
    public SecretKeyReader(ReadOnlySpan<byte> source)
    {
        Source = source;
    }

    /// <summary>
    /// Reads a <see cref="SymmetricSecretKey"/> from the source buffer.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <returns>The <see cref="SymmetricSecretKey"/> that was read.</returns>
    public SymmetricSecretKey ReadSymmetric(string keyId) => new(keyId, Source);

    private T ReadAsymmetricKey<T>(Func<T> factory, AsymmetricSecretKeyEncoding encoding, ImportPemDelegate<T> importPem)
        where T : AsymmetricAlgorithm =>
        encoding switch
        {
            AsymmetricSecretKeyEncoding.Unspecified => throw new InvalidOperationException(),
            AsymmetricSecretKeyEncoding.Pem => ReadPem(importPem),
            AsymmetricSecretKeyEncoding.Pkcs8 => ReadPkcs8(factory),
            _ => throw new InvalidOperationException()
        };

    private T ReadPkcs8<T>(Func<T> factory)
        where T : AsymmetricAlgorithm
    {
        var key = factory();
        try
        {
            key.ImportPkcs8PrivateKey(Source, out var bytesRead);
            Debug.Assert(bytesRead == Source.Length);
        }
        catch
        {
            key.Dispose();
            throw;
        }

        return key;
    }
}
