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
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Logic;

/// <summary>
/// Provides methods that operate on <see cref="Secret"/>.
/// </summary>
public interface ISecretService
{
    /// <summary>
    /// Given a collection of <see cref="Secret"/> items, converts and loads them into a disposable collection of
    /// <see cref="SecretKey"/> items.
    /// </summary>
    /// <param name="secrets">The secrets to convert and load.</param>
    /// <returns>The collection of <see cref="SecretKey"/> items.</returns>
    ISecretKeyCollection LoadSecretKeys(IEnumerable<Secret> secrets);
}

internal class SecretService : ISecretService
{
    /// <inheritdoc />
    public ISecretKeyCollection LoadSecretKeys(IEnumerable<Secret> secrets)
    {
        var list = new List<SecretKey>();
        try
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            // Nope, that is not equivalent. The try/catch is important.
            foreach (var secret in secrets)
            {
                list.Add(LoadSecretKey(secret));
            }
        }
        catch
        {
            foreach (var item in list)
            {
                try
                {
                    item.Dispose();
                }
                catch
                {
                    // ignore
                }
            }

            throw;
        }

        return new SecretKeyCollection(list);
    }

    private static SecretKey LoadSecretKey(Secret secret) =>
        secret.SecretType switch
        {
            SecretConstants.SecretTypes.Certificate => LoadCertificateSecurityKey(secret),
            SecretConstants.SecretTypes.Symmetric => LoadSymmetricSecurityKey(secret),
            SecretConstants.SecretTypes.Rsa => LoadRsaSecretKey(secret),
            SecretConstants.SecretTypes.Ecc => LoadEccSecretKey(secret),
            _ => throw new InvalidOperationException()
        };

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

    private static SecretKey LoadCertificateSecurityKey(Secret secret)
    {
        using var _ = CreatePemReader(secret, out var reader);
        return reader.ReadCertificate(secret.SecretId, secret.Tags);
    }

    private static SecretKey LoadSymmetricSecurityKey(Secret secret) =>
        secret.EncodingType switch
        {
            SecretConstants.EncodingTypes.None => new SymmetricSecretKey(secret.SecretId, secret.Tags, secret.EncodedValue),
            SecretConstants.EncodingTypes.Base64 => new SymmetricSecretKey(secret.SecretId, secret.Tags, Convert.FromBase64String(secret.EncodedValue)),
            _ => throw new InvalidOperationException("Invalid encoding type.")
        };

    private static SecretKey LoadRsaSecretKey(Secret secret)
    {
        using var _ = CreatePemReader(secret, out var reader);
        return reader.ReadRsa(secret.SecretId, secret.Tags, AsymmetricSecretKeyEncoding.Pem);
    }

    private static SecretKey LoadEccSecretKey(Secret secret)
    {
        using var _ = CreatePemReader(secret, out var reader);
        return reader.ReadEcc(secret.SecretId, secret.Tags, AsymmetricSecretKeyEncoding.Pem);
    }
}
