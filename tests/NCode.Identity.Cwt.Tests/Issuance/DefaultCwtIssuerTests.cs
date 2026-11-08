// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cwt.Issuance;

namespace NCode.Identity.Cwt.Tests.Issuance;

public class DefaultCwtIssuerTests
{
    private static CwtIssuanceParameters ExampleParameters(
        CwtTestHost host,
        CoseCredentials? credentials = null
    ) =>
        new()
        {
            Credentials =
                credentials ?? new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            Issuer = "coap://as.example.com",
            Subject = "erikw",
            Audiences = ["coap://light.example.com"],
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(1444064944),
            NotBefore = DateTimeOffset.FromUnixTimeSeconds(1443944944),
            IssuedAt = DateTimeOffset.FromUnixTimeSeconds(1443944944),
            CwtId = CwtTestHost.Hex("0B71"),
        };

    private static CoseVerifiedMessage VerifyEc(CwtTestHost host, byte[] token) =>
        host.Serializer.Verify(
            token,
            new CoseVerificationParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
                Keys = [host.EcPublicKey()],
            }
        );

    #region Issue Tests

    [Fact]
    public async Task Issue_SignatureCredentials_ProducesASign1WithTheExampleClaims()
    {
        await using var host = new CwtTestHost();

        var token = await host.Issuer.IssueAsync(ExampleParameters(host), CancellationToken.None);

        var verified = VerifyEc(host, token);
        Assert.Equal(CoseMessageType.Sign1, verified.MessageType);
        Assert.Equal(CwtTestHost.Hex(CwtTestHost.ClaimsA1), verified.Content.ToArray());
    }

    [Fact]
    public async Task Issue_MacCredentials_ProducesAMac0()
    {
        await using var host = new CwtTestHost();
        var parameters = ExampleParameters(
            host,
            new CoseCredentials(host.Hmac(), CoseAlgorithms.HmacSha256)
        );

        var token = await host.Issuer.IssueAsync(parameters, CancellationToken.None);

        Assert.Equal(CoseMessageType.Mac0, host.Serializer.Inspect(token, null).MessageType);
    }

    [Fact]
    public async Task Issue_NoIssuedAt_WritesTheCurrentTime()
    {
        await using var host = new CwtTestHost();
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
        };

        var token = await host.Issuer.IssueAsync(parameters, CancellationToken.None);

        var claims = host.Serializer.DecodeMap(VerifyEc(host, token).Content);
        Assert.True(claims[CwtClaimKeys.IssuedAt].TryGetInt64(out var issuedAt));
        Assert.Equal(CwtTestHost.StartTime.ToUnixTimeSeconds(), issuedAt);
    }

    [Fact]
    public async Task Issue_SeveralAudiences_WritesAnArray()
    {
        await using var host = new CwtTestHost();
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            Audiences = ["a", "b"],
        };

        var token = await host.Issuer.IssueAsync(parameters, CancellationToken.None);

        var claims = host.Serializer.DecodeMap(VerifyEc(host, token).Content);
        Assert.True(claims[CwtClaimKeys.Audience].TryGetArray(out var audiences));
        Assert.Equal(2, audiences.Count);
    }

    [Fact]
    public async Task Issue_TokenType_WritesTheProtectedTypeHeader()
    {
        await using var host = new CwtTestHost();
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            TokenType = "application/example+cwt",
        };

        var token = await host.Issuer.IssueAsync(parameters, CancellationToken.None);

        var info = host.Serializer.Inspect(token, null);
        Assert.True(info.ProtectedHeaders[CoseHeaderLabels.Type].TryGetTextString(out var type));
        Assert.Equal("application/example+cwt", type);
    }

    [Fact]
    public async Task Issue_ConfirmationKey_WritesOnlyThePublicKey()
    {
        await using var host = new CwtTestHost();
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            ConfirmationKey = host.EcPrivateKey("holder"),
        };

        var token = await host.Issuer.IssueAsync(parameters, CancellationToken.None);

        var claims = host.Serializer.DecodeMap(VerifyEc(host, token).Content);
        var confirmation = host.Serializer.DecodeMap(
            claims[CwtClaimKeys.Confirmation].EncodedValue
        );
        var key = host.Serializer.DecodeMap(confirmation[CwtConfirmationKeys.CoseKey].EncodedValue);
        Assert.False(key.ContainsKey(-4));
        Assert.True(key[-2].TryGetByteString(out var x));
        Assert.Equal(CwtTestHost.Hex(CwtTestHost.EcX), x.ToArray());
    }

    [Fact]
    public async Task Issue_EncryptionCredentials_WrapsTheTokenInAnEncrypt0()
    {
        await using var host = new CwtTestHost();
        var key = host.Aes("enc");
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            EncryptionCredentials = new CoseCredentials(key, CoseAlgorithms.A128Gcm),
        };

        var token = await host.Issuer.IssueAsync(parameters, CancellationToken.None);

        Assert.Equal(CoseMessageType.Encrypt0, host.Serializer.Inspect(token, null).MessageType);
        var decrypted = host.Serializer.Decrypt(
            token,
            new CoseDecryptionParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm],
                Keys = [key],
            }
        );
        var inner = host.Serializer.Inspect(decrypted.Content, null);
        Assert.Equal(CoseMessageType.Sign1, inner.MessageType);
        Assert.True(inner.IsTagged);
    }

    [Fact]
    public async Task Issue_EncryptionRecipients_WrapsTheTokenInAnEncrypt()
    {
        await using var host = new CwtTestHost();
        var key = host.Aes("enc");
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            EncryptionRecipients = new CoseRecipients(
                CoseAlgorithms.A128Gcm,
                [new CoseRecipient(new CoseCredentials(key, CoseAlgorithms.A128Kw))]
            ),
        };

        var token = await host.Issuer.IssueAsync(parameters, CancellationToken.None);

        Assert.Equal(CoseMessageType.Encrypt, host.Serializer.Inspect(token, null).MessageType);
        var decrypted = host.Serializer.Decrypt(
            token,
            new CoseDecryptionParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm],
                ValidKeyDistributionAlgorithms = [CoseAlgorithmIdentifiers.A128Kw],
                Keys = [key],
            }
        );
        Assert.Equal(
            CoseMessageType.Sign1,
            host.Serializer.Inspect(decrypted.Content, null).MessageType
        );
    }

    [Fact]
    public async Task Issue_WriteCwtTag_PrefixesTag61()
    {
        await using var host = new CwtTestHost();
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            WriteCwtTag = true,
        };

        var token = await host.Issuer.IssueAsync(parameters, CancellationToken.None);

        Assert.Equal("D83D", Convert.ToHexString(token.AsSpan(0, 2)));
        Assert.Equal(
            CoseMessageType.Sign1,
            host.Serializer.Inspect(token.AsMemory(2), null).MessageType
        );
    }

    [Fact]
    public async Task Issue_AdditionalClaimOwnedByATypedMember_Throws()
    {
        await using var host = new CwtTestHost();
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            AdditionalClaims = new Dictionary<CoseLabel, CoseValue>
            {
                [CwtClaimKeys.ExpirationTime] = CoseValue.FromInt64(1),
            },
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await host.Issuer.IssueAsync(parameters, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Issue_AdditionalClaim_IsWritten()
    {
        await using var host = new CwtTestHost();
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            AdditionalClaims = new Dictionary<CoseLabel, CoseValue>
            {
                [CwtClaimKeys.Scope] = CoseValue.FromTextString("read"),
            },
        };

        var token = await host.Issuer.IssueAsync(parameters, CancellationToken.None);

        var claims = host.Serializer.DecodeMap(VerifyEc(host, token).Content);
        Assert.True(claims[CwtClaimKeys.Scope].TryGetTextString(out var scope));
        Assert.Equal("read", scope);
    }

    [Fact]
    public async Task Issue_BothEncryptionOptions_Throws()
    {
        await using var host = new CwtTestHost();
        var key = host.Aes("enc");
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            EncryptionCredentials = new CoseCredentials(key, CoseAlgorithms.A128Gcm),
            EncryptionRecipients = new CoseRecipients(
                CoseAlgorithms.A128Gcm,
                [new CoseRecipient(new CoseCredentials(key, CoseAlgorithms.A128Kw))]
            ),
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await host.Issuer.IssueAsync(parameters, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Issue_ExpirationBeforeNotBefore_Throws()
    {
        await using var host = new CwtTestHost();
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            NotBefore = CwtTestHost.StartTime,
            ExpiresAt = CwtTestHost.StartTime - TimeSpan.FromMinutes(1),
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await host.Issuer.IssueAsync(parameters, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Issue_ExpirationAtTheIssuedAt_Throws()
    {
        await using var host = new CwtTestHost();
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            IssuedAt = CwtTestHost.StartTime,
            ExpiresAt = CwtTestHost.StartTime,
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await host.Issuer.IssueAsync(parameters, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Issue_EncryptionAlgorithmAsCredentials_Throws()
    {
        await using var host = new CwtTestHost();
        var parameters = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.Aes(), CoseAlgorithms.A128Gcm),
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await host.Issuer.IssueAsync(parameters, CancellationToken.None)
        );
    }

    #endregion
}
