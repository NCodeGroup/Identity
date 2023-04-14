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
using NIdentity.OpenId.Cryptography.CryptoProvider;
using NIdentity.OpenId.Cryptography.CryptoProvider.Aes;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Cryptography.Aes;

public class AesKeyWrapTests : BaseTests
{
    // the following test values where generated here:
    // https://asecuritysite.com/symmetric/kek

    // 0x000102030405060708090A0B0C0D0E0F
    private const string AesKeyB64 = "AAECAwQFBgcICQoLDA0ODw==";

    // 0x00112233445566778899AABBCCDDEEFF
    private const string ExpectedPlainText = "ABEiM0RVZneImaq7zN3u/w==";

    // 0x1fa68b0a8112b447aef34bd8fb5a7b829d3e862371d2cfe5
    private const string ExpectedCipherText = "H6aLCoEStEeu80vY+1p7gp0+hiNx0s/l";

    private ReadOnlyMemory<byte> AesKey { get; } = Convert.FromBase64String(AesKeyB64);
    private IAesKeyWrap AesKeyWrap { get; } = new AesKeyWrap();

    public AesKeyWrapTests()
    {
        CreateStrictMock<ICryptoFactory>();
    }

    [Fact]
    public void WrapKey_Valid()
    {
        var cek = Convert.FromBase64String(ExpectedPlainText);
        var cipherText = AesKeyWrap.WrapKey(AesKey.Span, cek);

        Assert.Equal(sizeof(long), cipherText.Length - cek.Length);
        Assert.Equal(ExpectedCipherText, Convert.ToBase64String(cipherText.ToArray()));
    }

    [Fact]
    public void UnwrapKey_Valid()
    {
        var encryptedCek = Convert.FromBase64String(ExpectedCipherText);
        var plainText = AesKeyWrap.UnwrapKey(AesKey.Span, encryptedCek);

        Assert.Equal(sizeof(long), encryptedCek.Length - plainText.Length);
        Assert.Equal(ExpectedPlainText, Convert.ToBase64String(plainText.ToArray()));
    }

    [Fact]
    public void RoundTrip_Valid()
    {
        const int anyKeySize = 312;
        var cek = GC.AllocateUninitializedArray<byte>(anyKeySize);
        Random.Shared.NextBytes(cek);

        var encryptedCek = AesKeyWrap.WrapKey(AesKey.Span, cek);
        var actualPlainText = AesKeyWrap.UnwrapKey(AesKey.Span, encryptedCek.ToArray());

        Assert.Equal(
            Convert.ToBase64String(cek),
            Convert.ToBase64String(actualPlainText.ToArray()));
    }
}
