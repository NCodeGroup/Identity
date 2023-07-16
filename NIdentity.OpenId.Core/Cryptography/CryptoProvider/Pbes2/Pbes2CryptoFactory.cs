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
using System.Text;
using NCode.PasswordGenerator;
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.CryptoProvider.Aes;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Pbes2.Descriptors;
using NIdentity.OpenId.Cryptography.Keys;
using NIdentity.OpenId.Cryptography.Keys.Material;

namespace NIdentity.OpenId.Cryptography.CryptoProvider.Pbes2;

/// <summary>
/// Provides factory methods to create providers for <c>PBES2</c> cryptographic algorithms.
/// </summary>
public class Pbes2CryptoFactory : SymmetricCryptoFactory<Pbes2CryptoFactory>
{
    /// <inheritdoc />
    protected override unsafe KeyMaterial GenerateKeyMaterial(int keySizeBits)
    {
        var charLength = keySizeBits / BinaryUtility.BitsPerByte;
        var charLease = ArrayPool<char>.Shared.Rent(charLength);
        try
        {
            // ReSharper disable once UnusedVariable
            fixed (char* charPinned = charLease)
            {
                var password = charLease.AsSpan(0, charLength);
                try
                {
                    PasswordGenerator.Default.Generate(options => options.SetExactLength(charLength), password);

                    var byteLength = Encoding.UTF8.GetByteCount(password);
                    var byteLease = CryptoPool.Rent(byteLength);
                    try
                    {
                        var span = byteLease.Memory.Span;
                        Encoding.UTF8.GetBytes(password, span);
                        return new SymmetricKeyMaterial(span);
                    }
                    catch
                    {
                        byteLease.Dispose();
                        throw;
                    }
                }
                finally
                {
                    password.Clear();
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(charLease);
        }
    }

    /// <inheritdoc />
    public override KeyWrapProvider CreateKeyWrapProvider(SecretKey secretKey, KeyWrapAlgorithmDescriptor descriptor)
    {
        KeySizesUtility.AssertLegalSize(secretKey, descriptor);

        var typedSecretKey = ValidateSecretKey<SymmetricSecretKey>(secretKey);
        var typedDescriptor = ValidateDescriptor<Pbes2KeyWrapAlgorithmDescriptor>(descriptor);

        return new Pbes2KeyWrapProvider(AesKeyWrap.Default, typedSecretKey, typedDescriptor);
    }
}
