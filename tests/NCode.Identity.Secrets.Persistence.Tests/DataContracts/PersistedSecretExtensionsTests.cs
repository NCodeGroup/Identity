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
            ActivatesWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = "test",
            SecretEncoding = secretEncoding,
            Origin = NCode.Identity.Secrets.Persistence.SecretOrigins.Generated,
            KeySizeBits = 256,
            EncodedValue = "value",
            PublicKey = publicKey,
        };

        Assert.Equal(expected, secret.HasPublicKey());
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static PersistedSecret CreateSecret(
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen,
        DateTimeOffset expiresWhen
    ) =>
        new()
        {
            SecretId = "secret-1",
            Use = "sig",
            Algorithm = null,
            CreatedWhen = Now.AddDays(-100),
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = expiresWhen,
            SecretType = "test",
            SecretEncoding = SecretEncodings.Pkcs8,
            Origin = NCode.Identity.Secrets.Persistence.SecretOrigins.Generated,
            KeySizeBits = 256,
            EncodedValue = "value",
        };

    [Fact]
    public void GetLifecycleState_BeforeTheActivation_IsUpcoming()
    {
        var secret = CreateSecret(Now.AddSeconds(1), Now.AddDays(90), Now.AddDays(97));

        Assert.Equal(SecretLifecycleStates.Upcoming, secret.GetLifecycleState(Now));
        Assert.False(secret.IsActive(Now));
        Assert.False(secret.IsExpired(Now));
    }

    [Fact]
    public void GetLifecycleState_InsideTheActiveWindow_IsActive()
    {
        var secret = CreateSecret(Now, Now.AddSeconds(1), Now.AddDays(7));

        Assert.Equal(SecretLifecycleStates.Active, secret.GetLifecycleState(Now));
        Assert.True(secret.IsActive(Now));
    }

    [Fact]
    public void GetLifecycleState_AfterTheRetirementAndBeforeTheExpiry_IsRetired()
    {
        var secret = CreateSecret(Now.AddDays(-90), Now, Now.AddSeconds(1));

        Assert.Equal(SecretLifecycleStates.Retired, secret.GetLifecycleState(Now));
        Assert.False(secret.IsActive(Now));
        Assert.False(secret.IsExpired(Now));
    }

    [Fact]
    public void GetLifecycleState_AfterTheExpiry_IsExpired()
    {
        var secret = CreateSecret(Now.AddDays(-97), Now.AddDays(-7), Now);

        Assert.Equal(SecretLifecycleStates.Expired, secret.GetLifecycleState(Now));
        Assert.True(secret.IsExpired(Now));
    }
}
