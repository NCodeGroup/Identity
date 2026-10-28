#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using Xunit;

namespace NCode.Identity.Jwk.Tests;

public class JsonWebKeyVocabularyTests
{
    [Theory]
    [InlineData(JsonWebKeyUses.Signature, "sig")]
    [InlineData(JsonWebKeyUses.Encryption, "enc")]
    public void Uses_HaveTheirRegisteredValues(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(JsonWebKeyOperations.Sign, "sign")]
    [InlineData(JsonWebKeyOperations.Verify, "verify")]
    [InlineData(JsonWebKeyOperations.Encrypt, "encrypt")]
    [InlineData(JsonWebKeyOperations.Decrypt, "decrypt")]
    [InlineData(JsonWebKeyOperations.WrapKey, "wrapKey")]
    [InlineData(JsonWebKeyOperations.UnwrapKey, "unwrapKey")]
    [InlineData(JsonWebKeyOperations.DeriveKey, "deriveKey")]
    [InlineData(JsonWebKeyOperations.DeriveBits, "deriveBits")]
    public void Operations_HaveTheirRegisteredValues(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(JsonWebKeyTypes.Rsa, "RSA")]
    [InlineData(JsonWebKeyTypes.Ec, "EC")]
    [InlineData(JsonWebKeyTypes.Okp, "OKP")]
    [InlineData(JsonWebKeyTypes.Oct, "oct")]
    [InlineData(JsonWebKeyTypes.Akp, "AKP")]
    public void KeyTypes_HaveTheirRegisteredValues(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(JsonWebKeySetParameterNames.Keys, "keys")]
    public void KeySetParameterNames_HaveTheirRegisteredValues(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }
}
