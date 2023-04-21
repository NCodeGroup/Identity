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
using NIdentity.OpenId.Cryptography.CryptoProvider.Aes;
using NIdentity.OpenId.Cryptography.CryptoProvider.Pbes2;
using NIdentity.OpenId.Cryptography.CryptoProvider.Pbes2.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Pbes2.Parameters;
using NIdentity.OpenId.Cryptography.Keys;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Cryptography.Pbes2;

public class Pbes2KeyWrapProviderTests : BaseTests
{
    [Fact]
    public void Pbkdf2_Valid()
    {
        const string password = "secret_password";
        const int iterationCount = 310000;
        const int keySizeBits = 128;

        var salt = Convert.FromBase64String("QxwJnJyShtDhBFdNunPNxQ==");
        var kek = new byte[keySizeBits / 8];

        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            kek,
            iterationCount,
            HashAlgorithmName.SHA256);

        Assert.Equal("mr1Q3V8dH47D+8K7/X2mEg==", Convert.ToBase64String(kek));
    }

    [Fact]
    public void WrapKey_Valid()
    {
        const string keyId = nameof(keyId);
        const int iterationCount = 8192;

        using var secretKey = new SymmetricSecretKey(keyId, "secret_password");

        var descriptor = new Pbes2KeyWrapAlgorithmDescriptor(
            AlgorithmCodes.KeyManagement.Pbes2HmacSha256Aes128,
            HashAlgorithmName.SHA256,
            HashSizeBits: 256,
            KeySizeBits: 128,
            DefaultIterationCount: 310000);

        var provider = new Pbes2KeyWrapProvider(AesKeyWrap.Default, secretKey, descriptor);

        var cek = Convert.FromBase64String("TCRe8k3T4sAmBEjuOdCLsQ==");
        var salt = Convert.FromBase64String("QxwJnJyShtDhBFdNunPNxQ==");

        var parameters = new Pbes2KeyWrapParameters(cek, salt, iterationCount);
        var encryptedContentKey = Convert.ToBase64String(provider.WrapKey(parameters).ToArray());
        Assert.Equal("mQzi+PmvyU2EHJC8NMn7jV5/uarXiCKq", encryptedContentKey);
    }

    [Theory]
    [ClassData(typeof(Pbes2RoundTripTestData))]
    public void RoundTrip_Valid(string password, Pbes2KeyWrapAlgorithmDescriptor descriptor)
    {
        const string keyId = nameof(keyId);

        using var secretKey = new SymmetricSecretKey(keyId, password);

        var provider = new Pbes2KeyWrapProvider(AesKeyWrap.Default, secretKey, descriptor);

        var expectedCek = new byte[descriptor.KeySizeBytes];
        RandomNumberGenerator.Fill(expectedCek);

        var salt = new byte[descriptor.KeySizeBytes];
        RandomNumberGenerator.Fill(salt);

        var encryptedContentKey = provider.WrapKey(new Pbes2KeyWrapParameters(expectedCek, salt));
        var actualCek = provider.UnwrapKey(new Pbes2KeyUnwrapParameters(encryptedContentKey.ToArray(), salt)).ToArray();

        Assert.Equal(Convert.ToBase64String(expectedCek), Convert.ToBase64String(actualCek));
    }
}
