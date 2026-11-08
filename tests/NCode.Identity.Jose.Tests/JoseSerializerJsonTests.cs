// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests;

public class JoseSerializerJsonTests : BaseTests
{
    private const string Payload = """{"foo":"bar","n":123,"flag":true}""";

    private ServiceProvider ServiceProvider { get; }
    private IJoseSerializer JoseSerializer { get; }
    private IAlgorithmCollection AlgorithmCollection { get; }

    public JoseSerializerJsonTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        JoseSerializer = ServiceProvider.GetRequiredService<IJoseSerializer>();
        AlgorithmCollection = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    #region Helpers

    private SignatureAlgorithm GetSignatureAlgorithm(string code)
    {
        Assert.True(AlgorithmCollection.TryGetSignatureAlgorithm(code, out var algorithm));
        return algorithm;
    }

    private KeyManagementAlgorithm GetKeyManagementAlgorithm(string code)
    {
        Assert.True(AlgorithmCollection.TryGetKeyManagementAlgorithm(code, out var algorithm));
        return algorithm;
    }

    private AuthenticatedEncryptionAlgorithm GetAuthenticatedEncryptionAlgorithm(string code)
    {
        Assert.True(
            AlgorithmCollection.TryGetAuthenticatedEncryptionAlgorithm(code, out var algorithm)
        );
        return algorithm;
    }

    private CompressionAlgorithm GetCompressionAlgorithm(string code)
    {
        Assert.True(AlgorithmCollection.TryGetCompressionAlgorithm(code, out var algorithm));
        return algorithm;
    }

    private static SecretKey CreateRsaKey(string keyId) =>
        SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = keyId }, RSA.Create(2048));

    private static SecretKey CreateSymmetricKey(string keyId, int bitCount)
    {
        var bytes = new byte[bitCount >> 3];
        RandomNumberGenerator.Fill(bytes);
        return SecretKeyFactory.CreateSymmetric(new KeyMetadata { KeyId = keyId }, bytes);
    }

    private JoseSigningCredentials RsaSigningCredentials(string keyId) =>
        new(CreateRsaKey(keyId), GetSignatureAlgorithm(AlgorithmCodes.DigitalSignature.RsaSha256));

    private JoseSigningCredentials HmacSigningCredentials(string keyId) =>
        new(
            CreateSymmetricKey(keyId, 256),
            GetSignatureAlgorithm(AlgorithmCodes.DigitalSignature.HmacSha256)
        );

    #endregion

    #region JWS JSON

    [Fact]
    public async Task EncodeJws_FlattenedSingleSignature_RoundTrips()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.Flattened,
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var jsonElement = JsonElement.Parse(token);
        var root = jsonElement;
        Assert.True(root.TryGetProperty("payload", out _));
        Assert.True(root.TryGetProperty("protected", out _));
        Assert.True(root.TryGetProperty("signature", out _));
        Assert.False(root.TryGetProperty("signatures", out _));

        var parsed = JoseSerializer.Parse(token);
        var decoded = await JoseSerializer.DecodeAsync(
            parsed,
            credentials.SecretKey,
            CancellationToken.None
        );
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
    }

    [Fact]
    public async Task EncodeJws_GeneralSingleSignature_RoundTrips()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.General,
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var jsonElement = JsonElement.Parse(token);
        var signatures = jsonElement.GetProperty("signatures");
        Assert.Equal(JsonValueKind.Array, signatures.ValueKind);
        Assert.Equal(1, signatures.GetArrayLength());

        var decoded = await JoseSerializer.DecodeAsync(
            token,
            credentials.SecretKey,
            CancellationToken.None
        );
        Assert.Equal(Payload, decoded);
    }

    [Fact]
    public async Task EncodeJws_GeneralMultipleSignatures_RoundTripsWithEachKey()
    {
        var rsa = RsaSigningCredentials("rsa-1");
        var hmac = HmacSigningCredentials("hmac-1");

        var options = new JoseSigningOptions
        {
            Format = JoseSerializationFormat.General,
            Signatures = [new JoseSignatureOptions(rsa), new JoseSignatureOptions(hmac)],
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var jsonElement = JsonElement.Parse(token);
        Assert.Equal(2, jsonElement.GetProperty("signatures").GetArrayLength());

        Assert.Equal(
            Payload,
            await JoseSerializer.DecodeAsync(token, rsa.SecretKey, CancellationToken.None)
        );
        Assert.Equal(
            Payload,
            await JoseSerializer.DecodeAsync(token, hmac.SecretKey, CancellationToken.None)
        );
    }

    [Fact]
    public async Task EncodeJws_WithUnprotectedHeader_PreservedAndRoundTrips()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(
            new JoseSignatureOptions(credentials)
            {
                UnprotectedHeaders = new Dictionary<string, object> { ["kid"] = "rsa-1" },
            }
        )
        {
            Format = JoseSerializationFormat.Flattened,
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var jsonElement = JsonElement.Parse(token);
        var unprotected = jsonElement.GetProperty("header");
        Assert.Equal("rsa-1", unprotected.GetProperty("kid").GetString());

        var parsed = JoseSerializer.Parse(token);
        var decoded = await JoseSerializer.DecodeAsync(
            parsed,
            credentials.SecretKey,
            CancellationToken.None
        );
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.Equal("rsa-1", header.GetProperty("kid").GetString());
    }

    [Fact]
    public async Task EncodeJws_DetachedPayload_OmitsPayloadMember()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.Flattened,
            DetachPayload = true,
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var jsonElement = JsonElement.Parse(token);
        Assert.False(jsonElement.TryGetProperty("payload", out _));
    }

    [Fact]
    public async Task EncodeJws_UnencodedPayload_RoundTrips()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.General,
            EncodePayload = false,
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var parsed = JoseSerializer.Parse(token);
        var decoded = await JoseSerializer.DecodeAsync(
            parsed,
            credentials.SecretKey,
            CancellationToken.None
        );
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.False(header.GetProperty("b64").GetBoolean());
    }

    [Fact]
    public async Task EncodeJws_CompactUnencodedNonAsciiPayload_RoundTrips()
    {
        const string NonAsciiPayload = "{\"name\":\"café\"}";

        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.Compact,
            EncodePayload = false,
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(NonAsciiPayload, CancellationToken.None);

        var parsed = JoseSerializer.Parse(token);
        var decoded = await JoseSerializer.DecodeAsync(
            parsed,
            credentials.SecretKey,
            CancellationToken.None
        );
        Assert.Equal(NonAsciiPayload, decoded);
    }

    [Fact]
    public async Task EncodeJws_CompactFormatWithMultipleSignatures_Throws()
    {
        var options = new JoseSigningOptions
        {
            Format = JoseSerializationFormat.Compact,
            Signatures =
            [
                new JoseSignatureOptions(RsaSigningCredentials("rsa-1")),
                new JoseSignatureOptions(HmacSigningCredentials("hmac-1")),
            ],
        };

        await Assert.ThrowsAsync<JoseException>(async () =>
            await JoseSerializer.CreateEncoder(options).EncodeAsync(Payload, CancellationToken.None)
        );
    }

    [Fact]
    public async Task EncodeJws_FlattenedWithMultipleSignatures_Throws()
    {
        var options = new JoseSigningOptions
        {
            Format = JoseSerializationFormat.Flattened,
            Signatures =
            [
                new JoseSignatureOptions(RsaSigningCredentials("rsa-1")),
                new JoseSignatureOptions(HmacSigningCredentials("hmac-1")),
            ],
        };

        await Assert.ThrowsAsync<JoseException>(async () =>
            await JoseSerializer.CreateEncoder(options).EncodeAsync(Payload, CancellationToken.None)
        );
    }

    [Fact]
    public async Task DecodeJws_WrongKey_Throws()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.Flattened,
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var wrongKey = CreateRsaKey("rsa-2");
        await Assert.ThrowsAsync<JoseIntegrityException>(async () =>
            await JoseSerializer.DecodeAsync(token, wrongKey, CancellationToken.None)
        );
    }

    #endregion

    #region JWE JSON

    private JoseRecipientOptions RsaRecipient(string keyId) =>
        new(
            new JoseKeyManagementCredentials(
                CreateRsaKey(keyId),
                GetKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.RsaOaep)
            )
        );

    private JoseRecipientOptions AesKwRecipient(string keyId) =>
        new(
            new JoseKeyManagementCredentials(
                CreateSymmetricKey(keyId, 256),
                GetKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.Aes256)
            )
        );

    [Fact]
    public async Task EncodeJwe_FlattenedSingleRecipient_RoundTrips()
    {
        var recipient = RsaRecipient("rsa-1");
        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.Flattened,
            Recipients = [recipient],
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var jsonElement = JsonElement.Parse(token);
        var root = jsonElement;
        Assert.True(root.TryGetProperty("protected", out _));
        Assert.True(root.TryGetProperty("encrypted_key", out _));
        Assert.True(root.TryGetProperty("iv", out _));
        Assert.True(root.TryGetProperty("ciphertext", out _));
        Assert.True(root.TryGetProperty("tag", out _));
        Assert.False(root.TryGetProperty("recipients", out _));

        var parsed = JoseSerializer.Parse(token);
        var decoded = await JoseSerializer.DecodeAsync(
            parsed,
            recipient.KeyManagementCredentials.SecretKey,
            CancellationToken.None
        );
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.Equal("RSA-OAEP", header.GetProperty("alg").GetString());
        Assert.Equal("A256GCM", header.GetProperty("enc").GetString());
    }

    [Fact]
    public async Task EncodeJwe_GeneralMultipleRecipients_RoundTripsWithEachKey()
    {
        var rsa = RsaRecipient("rsa-1");
        var aes = AesKwRecipient("aes-1");

        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.General,
            Recipients = [rsa, aes],
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var jsonElement = JsonElement.Parse(token);
        Assert.Equal(2, jsonElement.GetProperty("recipients").GetArrayLength());

        Assert.Equal(
            Payload,
            await JoseSerializer.DecodeAsync(
                token,
                rsa.KeyManagementCredentials.SecretKey,
                CancellationToken.None
            )
        );
        Assert.Equal(
            Payload,
            await JoseSerializer.DecodeAsync(
                token,
                aes.KeyManagementCredentials.SecretKey,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task EncodeJwe_WithSharedUnprotectedHeaderAndAad_RoundTrips()
    {
        var recipient = RsaRecipient("rsa-1");
        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.General,
            SharedUnprotectedHeaders = new Dictionary<string, object> { ["foo"] = "bar" },
            AdditionalAuthenticatedData = "extra-aad"u8.ToArray(),
            Recipients = [recipient],
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var jsonElement = JsonElement.Parse(token);
        Assert.True(jsonElement.TryGetProperty("aad", out _));
        Assert.Equal("bar", jsonElement.GetProperty("unprotected").GetProperty("foo").GetString());

        var parsed = JoseSerializer.Parse(token);
        var decoded = await JoseSerializer.DecodeAsync(
            parsed,
            recipient.KeyManagementCredentials.SecretKey,
            CancellationToken.None
        );
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.Equal("bar", header.GetProperty("foo").GetString());
    }

    [Fact]
    public async Task EncodeJwe_WithCompression_RoundTrips()
    {
        var recipient = RsaRecipient("rsa-1");
        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.Flattened,
            CompressionAlgorithm = GetCompressionAlgorithm(AlgorithmCodes.Compression.Deflate),
            Recipients = [recipient],
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var parsed = JoseSerializer.Parse(token);
        var decoded = await JoseSerializer.DecodeAsync(
            parsed,
            recipient.KeyManagementCredentials.SecretKey,
            CancellationToken.None
        );
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.Equal("DEF", header.GetProperty("zip").GetString());
    }

    [Fact]
    public async Task EncodeJwe_DirectAlgorithmFlattened_RoundTrips()
    {
        var key = CreateSymmetricKey("dir-1", 256);
        var recipient = new JoseRecipientOptions(
            new JoseKeyManagementCredentials(
                key,
                GetKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.Direct)
            )
        );

        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.Flattened,
            Recipients = [recipient],
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        Assert.Equal(Payload, await JoseSerializer.DecodeAsync(token, key, CancellationToken.None));
    }

    [Fact]
    public async Task EncodeJwe_CompactFormatWithMultipleRecipients_Throws()
    {
        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.Compact,
            Recipients = [RsaRecipient("rsa-1"), AesKwRecipient("aes-1")],
        };

        await Assert.ThrowsAsync<JoseException>(async () =>
            await JoseSerializer.CreateEncoder(options).EncodeAsync(Payload, CancellationToken.None)
        );
    }

    [Fact]
    public async Task EncodeJwe_MultipleRecipientsWithDirectAlgorithm_Throws()
    {
        var directRecipient = new JoseRecipientOptions(
            new JoseKeyManagementCredentials(
                CreateSymmetricKey("dir-1", 256),
                GetKeyManagementAlgorithm(AlgorithmCodes.KeyManagement.Direct)
            )
        );

        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.General,
            Recipients = [RsaRecipient("rsa-1"), directRecipient],
        };

        await Assert.ThrowsAsync<JoseException>(async () =>
            await JoseSerializer.CreateEncoder(options).EncodeAsync(Payload, CancellationToken.None)
        );
    }

    [Fact]
    public async Task DecodeJwe_WrongKey_Throws()
    {
        var recipient = RsaRecipient("rsa-1");
        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.Flattened,
            Recipients = [recipient],
        };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(Payload, CancellationToken.None);

        var wrongKey = CreateRsaKey("rsa-2");
        await Assert.ThrowsAsync<JoseEncryptionException>(async () =>
            await JoseSerializer.DecodeAsync(token, wrongKey, CancellationToken.None)
        );
    }

    #endregion
}
