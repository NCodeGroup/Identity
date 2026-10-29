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
    public void EncodeJws_FlattenedSingleSignature_RoundTrips()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.Flattened,
        };

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var document = JsonElement.Parse(token);
        var root = document;
        Assert.True(root.TryGetProperty("payload", out _));
        Assert.True(root.TryGetProperty("protected", out _));
        Assert.True(root.TryGetProperty("signature", out _));
        Assert.False(root.TryGetProperty("signatures", out _));

        var parsed = JoseSerializer.Parse(token);
        var decoded = JoseSerializer.Decode(parsed, credentials.SecretKey);
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
    }

    [Fact]
    public void EncodeJws_GeneralSingleSignature_RoundTrips()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.General,
        };

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var document = JsonElement.Parse(token);
        var signatures = document.GetProperty("signatures");
        Assert.Equal(JsonValueKind.Array, signatures.ValueKind);
        Assert.Equal(1, signatures.GetArrayLength());

        var decoded = JoseSerializer.Decode(token, credentials.SecretKey);
        Assert.Equal(Payload, decoded);
    }

    [Fact]
    public void EncodeJws_GeneralMultipleSignatures_RoundTripsWithEachKey()
    {
        var rsa = RsaSigningCredentials("rsa-1");
        var hmac = HmacSigningCredentials("hmac-1");

        var options = new JoseSigningOptions
        {
            Format = JoseSerializationFormat.General,
            Signatures = [new JoseSignatureOptions(rsa), new JoseSignatureOptions(hmac)],
        };

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var document = JsonElement.Parse(token);
        Assert.Equal(2, document.GetProperty("signatures").GetArrayLength());

        Assert.Equal(Payload, JoseSerializer.Decode(token, rsa.SecretKey));
        Assert.Equal(Payload, JoseSerializer.Decode(token, hmac.SecretKey));
    }

    [Fact]
    public void EncodeJws_WithUnprotectedHeader_PreservedAndRoundTrips()
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

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var document = JsonElement.Parse(token);
        var unprotected = document.GetProperty("header");
        Assert.Equal("rsa-1", unprotected.GetProperty("kid").GetString());

        var parsed = JoseSerializer.Parse(token);
        var decoded = JoseSerializer.Decode(parsed, credentials.SecretKey);
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.Equal("rsa-1", header.GetProperty("kid").GetString());
    }

    [Fact]
    public void EncodeJws_DetachedPayload_OmitsPayloadMember()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.Flattened,
            DetachPayload = true,
        };

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var document = JsonElement.Parse(token);
        Assert.False(document.TryGetProperty("payload", out _));
    }

    [Fact]
    public void EncodeJws_UnencodedPayload_RoundTrips()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.General,
            EncodePayload = false,
        };

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var parsed = JoseSerializer.Parse(token);
        var decoded = JoseSerializer.Decode(parsed, credentials.SecretKey);
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.False(header.GetProperty("b64").GetBoolean());
    }

    [Fact]
    public void EncodeJws_CompactUnencodedNonAsciiPayload_RoundTrips()
    {
        const string NonAsciiPayload = "{\"name\":\"café\"}";

        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.Compact,
            EncodePayload = false,
        };

        var token = JoseSerializer.CreateEncoder(options).Encode(NonAsciiPayload);

        var parsed = JoseSerializer.Parse(token);
        var decoded = JoseSerializer.Decode(parsed, credentials.SecretKey);
        Assert.Equal(NonAsciiPayload, decoded);
    }

    [Fact]
    public void EncodeJws_CompactFormatWithMultipleSignatures_Throws()
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

        Assert.Throws<JoseException>(() => JoseSerializer.CreateEncoder(options).Encode(Payload));
    }

    [Fact]
    public void EncodeJws_FlattenedWithMultipleSignatures_Throws()
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

        Assert.Throws<JoseException>(() => JoseSerializer.CreateEncoder(options).Encode(Payload));
    }

    [Fact]
    public void DecodeJws_WrongKey_Throws()
    {
        var credentials = RsaSigningCredentials("rsa-1");
        var options = new JoseSigningOptions(new JoseSignatureOptions(credentials))
        {
            Format = JoseSerializationFormat.Flattened,
        };

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var wrongKey = CreateRsaKey("rsa-2");
        Assert.Throws<JoseIntegrityException>(() => JoseSerializer.Decode(token, wrongKey));
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
    public void EncodeJwe_FlattenedSingleRecipient_RoundTrips()
    {
        var recipient = RsaRecipient("rsa-1");
        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.Flattened,
            Recipients = [recipient],
        };

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var document = JsonElement.Parse(token);
        var root = document;
        Assert.True(root.TryGetProperty("protected", out _));
        Assert.True(root.TryGetProperty("encrypted_key", out _));
        Assert.True(root.TryGetProperty("iv", out _));
        Assert.True(root.TryGetProperty("ciphertext", out _));
        Assert.True(root.TryGetProperty("tag", out _));
        Assert.False(root.TryGetProperty("recipients", out _));

        var parsed = JoseSerializer.Parse(token);
        var decoded = JoseSerializer.Decode(parsed, recipient.KeyManagementCredentials.SecretKey);
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.Equal("RSA-OAEP", header.GetProperty("alg").GetString());
        Assert.Equal("A256GCM", header.GetProperty("enc").GetString());
    }

    [Fact]
    public void EncodeJwe_GeneralMultipleRecipients_RoundTripsWithEachKey()
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

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var document = JsonElement.Parse(token);
        Assert.Equal(2, document.GetProperty("recipients").GetArrayLength());

        Assert.Equal(Payload, JoseSerializer.Decode(token, rsa.KeyManagementCredentials.SecretKey));
        Assert.Equal(Payload, JoseSerializer.Decode(token, aes.KeyManagementCredentials.SecretKey));
    }

    [Fact]
    public void EncodeJwe_WithSharedUnprotectedHeaderAndAad_RoundTrips()
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

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var document = JsonElement.Parse(token);
        Assert.True(document.TryGetProperty("aad", out _));
        Assert.Equal("bar", document.GetProperty("unprotected").GetProperty("foo").GetString());

        var parsed = JoseSerializer.Parse(token);
        var decoded = JoseSerializer.Decode(parsed, recipient.KeyManagementCredentials.SecretKey);
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.Equal("bar", header.GetProperty("foo").GetString());
    }

    [Fact]
    public void EncodeJwe_WithCompression_RoundTrips()
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

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var parsed = JoseSerializer.Parse(token);
        var decoded = JoseSerializer.Decode(parsed, recipient.KeyManagementCredentials.SecretKey);
        var header = parsed.Header;
        Assert.Equal(Payload, decoded);
        Assert.Equal("DEF", header.GetProperty("zip").GetString());
    }

    [Fact]
    public void EncodeJwe_DirectAlgorithmFlattened_RoundTrips()
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

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        Assert.Equal(Payload, JoseSerializer.Decode(token, key));
    }

    [Fact]
    public void EncodeJwe_CompactFormatWithMultipleRecipients_Throws()
    {
        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.Compact,
            Recipients = [RsaRecipient("rsa-1"), AesKwRecipient("aes-1")],
        };

        Assert.Throws<JoseException>(() => JoseSerializer.CreateEncoder(options).Encode(Payload));
    }

    [Fact]
    public void EncodeJwe_MultipleRecipientsWithDirectAlgorithm_Throws()
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

        Assert.Throws<JoseException>(() => JoseSerializer.CreateEncoder(options).Encode(Payload));
    }

    [Fact]
    public void DecodeJwe_WrongKey_Throws()
    {
        var recipient = RsaRecipient("rsa-1");
        var options = new JoseEncryptionOptions(
            GetAuthenticatedEncryptionAlgorithm(AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm)
        )
        {
            Format = JoseSerializationFormat.Flattened,
            Recipients = [recipient],
        };

        var token = JoseSerializer.CreateEncoder(options).Encode(Payload);

        var wrongKey = CreateRsaKey("rsa-2");
        Assert.Throws<JoseEncryptionException>(() => JoseSerializer.Decode(token, wrongKey));
    }

    #endregion
}
