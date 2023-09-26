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
using System.Text;
using NCode.CryptoMemory;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Logic;

/// <summary>
/// Provides the ability to serialize and deserialize secrets.
/// </summary>
public interface ISecretSerializer
{
    /// <summary>
    /// Deserializes a collection of <see cref="Secret"/> instances by loading and converting them into a disposable collection of <see cref="SecretKey"/> instances.
    /// </summary>
    /// <param name="secrets">The <see cref="Secret"/> instances to deserialize into <see cref="SecretKey"/> instances.</param>
    /// <returns>The collection of <see cref="SecretKey"/> instances.</returns>
    ISecretKeyCollection DeserializeSecrets(IEnumerable<Secret> secrets);

    /// <summary>
    /// Deserializes a <see cref="Secret"/> instance by loading and converting it into a disposable <see cref="SecretKey"/> instance.
    /// </summary>
    /// <param name="secret">The <see cref="Secret"/> instance to deserialize into an <see cref="SecretKey"/> instance.</param>
    /// <returns>The deserialized <see cref="SecretKey"/> instance.</returns>
    SecretKey DeserializeSecret(Secret secret);
}

internal class SecretSerializer : ISecretSerializer
{
    /// <inheritdoc />
    public ISecretKeyCollection DeserializeSecrets(IEnumerable<Secret> secrets)
    {
        var list = new List<SecretKey>();
        try
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            // Nope, that is not equivalent. The try/catch is important.
            foreach (var secret in secrets)
            {
                list.Add(DeserializeSecret(secret));
            }
        }
        catch
        {
            list.DisposeAll(ignoreExceptions: true);
            throw;
        }

        return new SecretKeyCollection(list);
    }

    /// <inheritdoc />
    public SecretKey DeserializeSecret(Secret secret)
    {
        var metadata = new KeyMetadata(secret.SecretId, secret.Use, secret.Algorithm);
        return secret.SecretType switch
        {
            SecretConstants.SecretTypes.Certificate => DeserializeCertificate(metadata, secret),
            SecretConstants.SecretTypes.Symmetric => DeserializeSymmetric(metadata, secret),
            SecretConstants.SecretTypes.Rsa => DeserializeRsa(metadata, secret),
            SecretConstants.SecretTypes.Ecc => DeserializeEcc(metadata, secret),
            _ => throw new InvalidOperationException()
        };
    }

    private static SecretKey DeserializeCertificate(KeyMetadata metadata, Secret secret)
    {
        using var _ = CreatePemReader(secret, out var reader);
        return reader.ReadCertificate(metadata);
    }

    private static SecretKey DeserializeSymmetric(KeyMetadata metadata, Secret secret) =>
        secret.EncodingType switch
        {
            SecretConstants.EncodingTypes.None => new SymmetricSecretKey(metadata, secret.EncodedValue),
            SecretConstants.EncodingTypes.Base64 => new SymmetricSecretKey(metadata, Convert.FromBase64String(secret.EncodedValue)),
            _ => throw new InvalidOperationException($"The {secret.EncodingType} encoding type is not supported when deserializing a symmetric secret.")
        };

    private static SecretKey DeserializeRsa(KeyMetadata metadata, Secret secret)
    {
        using var _ = CreatePemReader(secret, out var reader);
        return reader.ReadRsa(metadata, AsymmetricSecretKeyEncoding.Pem);
    }

    private static SecretKey DeserializeEcc(KeyMetadata metadata, Secret secret)
    {
        using var _ = CreatePemReader(secret, out var reader);
        return reader.ReadEcc(metadata, AsymmetricSecretKeyEncoding.Pem);
    }

    private static IDisposable CreatePemReader(Secret secret, out SecretKeyReader reader)
    {
        if (secret.EncodingType != SecretConstants.EncodingTypes.Pem)
            throw new InvalidOperationException($"Loading an `{secret.SecretType}` secret requires PEM encoding.");

        var byteCount = Encoding.ASCII.GetByteCount(secret.EncodedValue);
        var lease = CryptoPool.Rent(byteCount, isSensitive: true, out Span<byte> bytes);
        try
        {
            var bytesWritten = Encoding.ASCII.GetBytes(secret.EncodedValue, bytes);
            Debug.Assert(bytesWritten == byteCount);

            reader = new SecretKeyReader(bytes);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }
}
