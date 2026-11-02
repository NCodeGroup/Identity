// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Tests.DataContracts;

public sealed class PersistedSecretExtensionsTests
{
    [Theory]
    [InlineData(SecretEncodings.Spki, null, true)]
    [InlineData(SecretEncodings.X509, null, true)]
    [InlineData(SecretEncodings.Pkcs8, "spki-value", true)]
    [InlineData(SecretEncodings.Pkcs8, null, false)]
    [InlineData(SecretEncodings.SymmetricRaw, null, false)]
    public void HasPublicKey_ReflectsTheEncodingAndStoredPublicKey(
        string secretEncoding,
        string? publicKey,
        bool expected
    )
    {
        var secret = new PersistedSecret
        {
            SecretId = "secret-1",
            Use = "sig",
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = "test",
            SecretEncoding = secretEncoding,
            KeySizeBits = 256,
            EncodedValue = "value",
            PublicKey = publicKey,
        };

        Assert.Equal(expected, secret.HasPublicKey());
    }
}
