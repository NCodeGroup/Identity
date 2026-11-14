// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Azure;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Moq;
using NCode.Identity.Secrets.AzureKeyVault.Facades;
using NCode.Identity.Secrets.AzureKeyVault.Options;
using NCode.Identity.Secrets.KeyWrapping;
using Xunit;

namespace NCode.Identity.Secrets.AzureKeyVault.Tests;

public class AzureKeyVaultKeyWrapperTests : BaseTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string KeyVersionId = KeyId + "/0123456789abcdef0123456789abcdef";

    private const string OtherKeyVersionId =
        "https://contoso.vault.azure.net/keys/other/0123456789abcdef0123456789abcdef";

    private const string TenantKeyId = "https://tenant.vault.azure.net/keys/tenant-root";

    private const string TenantKeyVersionId = TenantKeyId + "/fedcba9876543210fedcba9876543210";

    private const string DirectoryId = "11111111-2222-3333-4444-555555555555";

    private static KeyAccess Access { get; } = new(DirectoryId, "tenant:acme");

    private Mock<IAzureKeyVaultClientFactory> ClientFactory { get; }

    private Mock<CryptographyClient> Client { get; }

    private byte[]? SentArray { get; set; }

    private byte[]? SentKey { get; set; }

    private byte[]? SentCiphertext { get; set; }

    public AzureKeyVaultKeyWrapperTests()
    {
        ClientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
        Client = CreateStrictMock<CryptographyClient>();
    }

    private AzureKeyVaultKeyWrapper CreateProvider(
        string? keyId,
        string[]? referencedKeyHostSuffixes = null
    )
    {
        var options = new AzureKeyVaultKeyWrapperOptions { KeyId = keyId };
        if (referencedKeyHostSuffixes is not null)
            options.ReferencedKeyHostSuffixes = referencedKeyHostSuffixes;

        return new AzureKeyVaultKeyWrapper(
            ClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(options)
        );
    }

    private static WrapResult CreateWrapResult(string keyId, byte[] encryptedKey) =>
        CryptographyModelFactory.WrapResult(keyId, encryptedKey, KeyWrapAlgorithm.RsaOaep256);

    // The factory runs the operation of the provider with the client mock, as it runs it with a client of the key and
    // access.
    private void SetupExecute<TResult>(string keyId, KeyAccess? access) =>
        ClientFactory
            .Setup(x =>
                x.ExecuteAsync(
                    keyId,
                    access,
                    It.IsAny<Func<CryptographyClient, CancellationToken, ValueTask<TResult>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    string _,
                    KeyAccess? _,
                    Func<CryptographyClient, CancellationToken, ValueTask<TResult>> operation,
                    CancellationToken token
                ) => operation(Client.Object, token)
            )
            .Verifiable();

    private void SetupWrapClient(string keyId, KeyAccess? access = null) =>
        SetupExecute<WrapResult>(keyId, access);

    private void SetupUnwrapClient(string keyId, KeyAccess? access = null) =>
        SetupExecute<UnwrapResult>(keyId, access);

    // The provider zeroes the array it sends once the call returns, so the test keeps a copy of what was sent along with
    // the array itself.
    private void SetupWrapKey(WrapResult result, CancellationToken cancellationToken) =>
        Client
            .Setup(x =>
                x.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, It.IsAny<byte[]>(), cancellationToken)
            )
            .Callback<KeyWrapAlgorithm, byte[], CancellationToken>(
                (_, key, _) =>
                {
                    SentArray = key;
                    SentKey = key.ToArray();
                }
            )
            .ReturnsAsync(result)
            .Verifiable();

    private static UnwrapResult CreateUnwrapResult(string keyId, byte[] key) =>
        CryptographyModelFactory.UnwrapResult(keyId, key, KeyWrapAlgorithm.RsaOaep256);

    private static WrappedKey CreateWrappedKey(string keyId, byte[] ciphertext) =>
        new()
        {
            WrapperName = KeyWrapperNames.AzureKeyVault,
            KeyId = keyId,
            Ciphertext = Base64Url.EncodeToString(ciphertext),
        };

    private void SetupUnwrapKey(UnwrapResult result, CancellationToken cancellationToken) =>
        Client
            .Setup(x =>
                x.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, It.IsAny<byte[]>(), cancellationToken)
            )
            .Callback<KeyWrapAlgorithm, byte[], CancellationToken>(
                (_, encryptedKey, _) => SentCiphertext = encryptedKey.ToArray()
            )
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupUnwrapKeyThrows(Exception exception) =>
        Client
            .Setup(x =>
                x.UnwrapKeyAsync(
                    KeyWrapAlgorithm.RsaOaep256,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception)
            .Verifiable();

    private void SetupWrapKeyThrows(Exception exception) =>
        Client
            .Setup(x =>
                x.WrapKeyAsync(
                    KeyWrapAlgorithm.RsaOaep256,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception)
            .Verifiable();

    #region WrapAsync Tests

    [Fact]
    public async Task WrapAsync_ServiceWrapsKey_SendsKeyWithRsaOaep256AndReturnsWrappedKey()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        using var cancellationSource = new CancellationTokenSource();
        SetupWrapClient(KeyId);
        SetupWrapKey(CreateWrapResult(KeyVersionId, encryptedKey), cancellationSource.Token);
        var provider = CreateProvider(KeyId);

        var wrappedKey = await provider.WrapAsync(key, context, cancellationSource.Token);

        Assert.Equal(key, SentKey);
        Assert.Equal(KeyWrapperNames.AzureKeyVault, wrappedKey.WrapperName);
        Assert.Equal(KeyVersionId, wrappedKey.KeyId);
        Assert.Equal(Base64Url.EncodeToString(encryptedKey), wrappedKey.Ciphertext);
    }

    [Fact]
    public async Task WrapAsync_ServiceWrapsKey_ZeroesArraySentToService()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupWrapClient(KeyId);
        SetupWrapKey(CreateWrapResult(KeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(KeyId);

        await provider.WrapAsync(key, context, CancellationToken.None);

        var sentArray = Assert.IsType<byte[]>(SentArray);
        Assert.Equal(new byte[key.Length], sentArray);
    }

    [Fact]
    public async Task WrapAsync_KeyIdUnset_ThrowsInvalidOperationException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(keyId: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task WrapAsync_ResultKeyIdWithoutVersion_ThrowsInvalidOperationException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupWrapClient(KeyId);
        SetupWrapKey(CreateWrapResult(KeyId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task WrapAsync_WrappedKeyShorterThan3072Bits_ThrowsInvalidOperationException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(256);
        SetupWrapClient(KeyId);
        SetupWrapKey(CreateWrapResult(KeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task WrapAsync_ResultKeyIsAVersionOfAnotherKey_ThrowsInvalidOperationException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupWrapClient(KeyId);
        SetupWrapKey(CreateWrapResult(OtherKeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task WrapAsync_ConfiguredKeyOnAnotherHost_IsNotRestricted()
    {
        const string internalKeyId = "https://hsm.internal.corp/keys/root";
        const string internalKeyVersionId = internalKeyId + "/0123456789abcdef0123456789abcdef";
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupWrapClient(internalKeyId);
        SetupWrapKey(CreateWrapResult(internalKeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(internalKeyId);

        var wrappedKey = await provider.WrapAsync(key, context, CancellationToken.None);

        Assert.Equal(internalKeyVersionId, wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKey_SendsKeyThroughTheClientOfTheReference()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupWrapClient(TenantKeyId);
        SetupWrapKey(CreateWrapResult(TenantKeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(KeyId);

        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(TenantKeyId, null),
            key,
            context,
            CancellationToken.None
        );

        Assert.Equal(key, SentKey);
        Assert.Equal(KeyWrapperNames.AzureKeyVault, wrappedKey.WrapperName);
        Assert.Equal(TenantKeyVersionId, wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithVersion_AcceptsOnlyThatVersion()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupWrapClient(TenantKeyVersionId);
        SetupWrapKey(
            CreateWrapResult(TenantKeyId + "/ffffffffffffffffffffffffffffffff", encryptedKey),
            CancellationToken.None
        );
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantKeyVersionId, null),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyReportsVersionOfAnotherKey_ThrowsInvalidOperationException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupWrapClient(TenantKeyId);
        SetupWrapKey(CreateWrapResult(KeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantKeyId, null),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyShorterThan3072Bits_ThrowsInvalidOperationException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(256);
        SetupWrapClient(TenantKeyId);
        SetupWrapKey(CreateWrapResult(TenantKeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantKeyId, null),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyInvalid_ThrowsWithoutCallingTheClient()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(KeyId);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey("not-a-key", null),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("referencedKey", exception.ParamName);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyOnAnotherHost_ThrowsWithoutCallingTheClient()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(KeyId);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey("https://internal.corp/keys/x", null),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("referencedKey", exception.ParamName);
        Assert.Contains("whose host ends with one of", exception.Message);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithAccess_ReceivesAccessFromTheClientFactory()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupWrapClient(TenantKeyId, Access);
        SetupWrapKey(CreateWrapResult(TenantKeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(keyId: null);

        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(TenantKeyId, Access),
            key,
            context,
            CancellationToken.None
        );

        Assert.Equal(key, SentKey);
        Assert.Equal(TenantKeyVersionId, wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithoutAccess_ReceivesNoAccessFromTheClientFactory()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupWrapClient(TenantKeyId, access: null);
        SetupWrapKey(CreateWrapResult(TenantKeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(keyId: null);

        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(TenantKeyId, Access: null),
            key,
            context,
            CancellationToken.None
        );

        Assert.Equal(TenantKeyVersionId, wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithInvalidReference_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(KeyId);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey("not-a-key", Access),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("referencedKey", exception.ParamName);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithInvalidDirectoryId_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(KeyId);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantKeyId, new KeyAccess("not-a-directory", "tenant:acme")),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("referencedKey", exception.ParamName);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithAccessFails_Propagates()
    {
        var failure = new InvalidOperationException("The directory has not consented.");
        SetupWrapClient(TenantKeyId, Access);
        SetupWrapKeyThrows(failure);
        var provider = CreateProvider(keyId: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantKeyId, Access),
                    RandomNumberGenerator.GetBytes(32),
                    "root-context"u8.ToArray(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithoutAccessFails_Propagates()
    {
        var failure = new InvalidOperationException("The service is unavailable.");
        SetupWrapClient(TenantKeyId, access: null);
        SetupWrapKeyThrows(failure);
        var provider = CreateProvider(keyId: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantKeyId, Access: null),
                    RandomNumberGenerator.GetBytes(32),
                    "root-context"u8.ToArray(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithAccessIsCanceled_Propagates()
    {
        var failure = new OperationCanceledException();
        SetupWrapClient(TenantKeyId, Access);
        SetupWrapKeyThrows(failure);
        var provider = CreateProvider(keyId: null);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantKeyId, Access),
                    RandomNumberGenerator.GetBytes(32),
                    "root-context"u8.ToArray(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    #endregion

    #region TryValidateKeyReference Tests

    [Theory]
    [InlineData(KeyId, true)]
    [InlineData(KeyVersionId, true)]
    [InlineData("https://CONTOSO.VAULT.AZURE.NET/keys/ncode-identity-root", true)]
    [InlineData("https://x.managedhsm.azure.net/keys/tenant-root", true)]
    [InlineData("https://x.vault.azure.cn/keys/tenant-root", true)]
    [InlineData("https://x.managedhsm.usgovcloudapi.net/keys/tenant-root", true)]
    [InlineData("http://contoso.vault.azure.net/keys/ncode-identity-root", false)]
    [InlineData("not-a-key", false)]
    [InlineData("", false)]
    public void TryValidateKeyReference_Reference_ReportsSyntax(string reference, bool expected)
    {
        var provider = CreateProvider(keyId: null);

        var valid = provider.TryValidateKeyReference(reference, out var failure);

        Assert.Equal(expected, valid);
        Assert.Equal(expected, failure is null);
        if (!expected)
            Assert.StartsWith("must be the identifier of a Key Vault key", failure);
    }

    [Theory]
    [InlineData("https://internal.corp/keys/x")]
    [InlineData("https://vault.azure.net/keys/x")]
    [InlineData("https://contoso.vault.azure.net.evil.example/keys/x")]
    [InlineData("https://contoso-vault.azure.net/keys/x")]
    public void TryValidateKeyReference_HostOutsideTheSuffixes_FailsWithTheSuffixes(
        string reference
    )
    {
        var provider = CreateProvider(keyId: null);

        var valid = provider.TryValidateKeyReference(reference, out var failure);

        Assert.False(valid);
        Assert.StartsWith(
            "must name a key in an Azure Key Vault or Managed HSM, whose host ends with one of ",
            failure
        );
        Assert.Contains(".vault.azure.net, .managedhsm.azure.net", failure);
    }

    [Fact]
    public void TryValidateKeyReference_CustomSuffixes_AllowsItsHostOnly()
    {
        var provider = CreateProvider(keyId: null, [".corp.example"]);

        var custom = provider.TryValidateKeyReference(
            "https://hsm.corp.example/keys/x",
            out var customFailure
        );
        var standard = provider.TryValidateKeyReference(TenantKeyId, out var standardFailure);

        Assert.True(custom);
        Assert.Null(customFailure);
        Assert.False(standard);
        Assert.EndsWith(".corp.example", standardFailure);
    }

    #endregion

    #region TryValidateAccessIdentity Tests

    [Theory]
    [InlineData(DirectoryId, true)]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData("{11111111-2222-3333-4444-555555555555}", false)]
    [InlineData("11111111222233334444555555555555", false)]
    [InlineData("not-a-directory", false)]
    [InlineData("", false)]
    public void TryValidateAccessIdentity_Identity_ReportsSyntax(string identity, bool expected)
    {
        var provider = CreateProvider(keyId: null);

        var valid = provider.TryValidateAccessIdentity(identity, out var failure);

        Assert.Equal(expected, valid);
        Assert.Equal(expected, failure is null);
        if (!expected)
            Assert.Equal("must be a Microsoft Entra tenant ID, a GUID", failure);
    }

    #endregion

    #region GetConfigurationFailures Tests

    [Theory]
    [InlineData(KeyId)]
    [InlineData(KeyVersionId)]
    public void GetConfigurationFailures_WithAValidKeyId_ReturnsNone(string keyId)
    {
        var provider = CreateProvider(keyId);

        var failures = provider.GetConfigurationFailures();

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-key")]
    [InlineData("https://contoso.vault.azure.net/secrets/ncode-identity-root")]
    public void GetConfigurationFailures_WithAMalformedKeyId_ReturnsTheMessage(string? keyId)
    {
        var provider = CreateProvider(keyId);

        var failures = provider.GetConfigurationFailures();

        var failure = Assert.Single(failures);
        Assert.Equal(
            "SecretProtection:RootKeyProviders:AzureKeyVault:KeyId must be the identifier of a Key Vault or Managed HSM key, such as https://contoso.vault.azure.net/keys/ncode-identity-root.",
            failure
        );
    }

    #endregion

    #region GetReferenceConfigurationFailures Tests

    private const string SuffixesKey =
        "SecretProtection:RootKeyProviders:AzureKeyVault:ReferencedKeyHostSuffixes";

    [Fact]
    public void GetReferenceConfigurationFailures_WithTheDefaultSuffixes_ReturnsNone()
    {
        var provider = CreateProvider(KeyId);

        var failures = provider.GetReferenceConfigurationFailures();

        Assert.Empty(failures);
    }

    [Fact]
    public void GetReferenceConfigurationFailures_WithCustomSuffixes_ReturnsNone()
    {
        var provider = CreateProvider(KeyId, [".vault.example.net", ".hsm.example.net"]);

        var failures = provider.GetReferenceConfigurationFailures();

        Assert.Empty(failures);
    }

    [Fact]
    public void GetReferenceConfigurationFailures_WithNoSuffixes_ReturnsTheMessage()
    {
        var provider = CreateProvider(KeyId, []);

        var failures = provider.GetReferenceConfigurationFailures();

        var failure = Assert.Single(failures);
        Assert.Equal($"{SuffixesKey} must list at least one host suffix.", failure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void GetReferenceConfigurationFailures_WithABlankSuffix_ReturnsTheMessage(string suffix)
    {
        var provider = CreateProvider(KeyId, [".vault.azure.net", suffix]);

        var failures = provider.GetReferenceConfigurationFailures();

        var failure = Assert.Single(failures);
        Assert.Equal($"{SuffixesKey} must not contain a blank entry.", failure);
    }

    [Fact]
    public void GetReferenceConfigurationFailures_WithASuffixWithoutADot_ReturnsTheMessage()
    {
        var provider = CreateProvider(KeyId, [".vault.azure.net", "vault.azure.net"]);

        var failures = provider.GetReferenceConfigurationFailures();

        var failure = Assert.Single(failures);
        Assert.Equal($"{SuffixesKey} entry 'vault.azure.net' must start with a dot.", failure);
    }

    [Fact]
    public void GetReferenceConfigurationFailures_WithARepeatedSuffix_ReturnsTheMessage()
    {
        var provider = CreateProvider(KeyId, [".vault.azure.net", ".VAULT.azure.net"]);

        var failures = provider.GetReferenceConfigurationFailures();

        var failure = Assert.Single(failures);
        Assert.Equal($"{SuffixesKey} entry '.VAULT.azure.net' is listed more than once.", failure);
    }

    [Fact]
    public void GetReferenceConfigurationFailures_WithSeveralProblems_ReturnsEachMessage()
    {
        var provider = CreateProvider(KeyId, ["", "vault.azure.net"]);

        var failures = provider.GetReferenceConfigurationFailures();

        Assert.Equal(
            [
                $"{SuffixesKey} must not contain a blank entry.",
                $"{SuffixesKey} entry 'vault.azure.net' must start with a dot.",
            ],
            failures
        );
    }

    #endregion

    #region UnwrapAsync Tests

    [Fact]
    public async Task UnwrapAsync_SendsRecordedKeyVersionWithRsaOaep256_WritesPlaintextAndZeroesResultKey()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(384);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyVersionId, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var result = CreateUnwrapResult(KeyVersionId, plaintext.ToArray());
        using var cancellationSource = new CancellationTokenSource();
        SetupUnwrapClient(KeyVersionId);
        SetupUnwrapKey(result, cancellationSource.Token);
        var provider = CreateProvider(KeyId);

        await provider.UnwrapAsync(wrappedKey, context, destination, cancellationSource.Token);

        Assert.Equal(ciphertext, SentCiphertext);
        Assert.Equal(plaintext.Length, destination.WrittenCount);
        Assert.Equal(plaintext, destination.WrittenSpan.ToArray());
        Assert.Equal(new byte[plaintext.Length], result.Key);
    }

    [Fact]
    public async Task UnwrapAsync_ServiceRejectsCiphertext_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(384);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyVersionId, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var serviceException = new RequestFailedException(400, "x");
        SetupUnwrapClient(KeyVersionId);
        SetupUnwrapKeyThrows(serviceException);
        var provider = CreateProvider(KeyId);

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Same(serviceException, exception.InnerException);
    }

    [Fact]
    public async Task UnwrapAsync_ServiceUnavailable_Propagates()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(384);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyVersionId, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var serviceException = new RequestFailedException(503, "x");
        SetupUnwrapClient(KeyVersionId);
        SetupUnwrapKeyThrows(serviceException);
        var provider = CreateProvider(KeyId);

        var exception = await Assert.ThrowsAsync<RequestFailedException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Same(serviceException, exception);
    }

    [Fact]
    public async Task UnwrapAsync_KeyIdWithoutVersion_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(384);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyId, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("!!!!")]
    public async Task UnwrapAsync_MalformedCiphertext_ThrowsCryptographicException(
        string ciphertext
    )
    {
        var context = "root-context"u8.ToArray();
        var wrappedKey = new WrappedKey
        {
            WrapperName = KeyWrapperNames.AzureKeyVault,
            KeyId = KeyVersionId,
            Ciphertext = ciphertext,
        };
        var destination = new ArrayBufferWriter<byte>();
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task UnwrapAsync_KeyWrappedUnderReference_UnwrapsThroughItsVersion()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupWrapClient(TenantKeyId);
        SetupWrapKey(CreateWrapResult(TenantKeyVersionId, encryptedKey), CancellationToken.None);
        SetupUnwrapClient(TenantKeyVersionId);
        SetupUnwrapKey(
            CreateUnwrapResult(TenantKeyVersionId, key.ToArray()),
            CancellationToken.None
        );
        var provider = CreateProvider(keyId: null);
        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(TenantKeyId, null),
            key,
            context,
            CancellationToken.None
        );
        var destination = new ArrayBufferWriter<byte>();

        await provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None);

        Assert.Equal(encryptedKey, SentCiphertext);
        Assert.Equal(key, destination.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task UnwrapAsync_WithAccess_ReceivesAccessFromTheClientFactory()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(384);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(TenantKeyVersionId, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        SetupUnwrapClient(TenantKeyVersionId, Access);
        SetupUnwrapKey(
            CreateUnwrapResult(TenantKeyVersionId, plaintext.ToArray()),
            CancellationToken.None
        );
        var provider = CreateProvider(keyId: null);

        await provider.UnwrapAsync(
            wrappedKey,
            Access,
            context,
            destination,
            CancellationToken.None
        );

        Assert.Equal(ciphertext, SentCiphertext);
        Assert.Equal(plaintext, destination.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task UnwrapAsync_WithNullAccess_Throws()
    {
        var wrappedKey = CreateWrappedKey(KeyVersionId, RandomNumberGenerator.GetBytes(384));
        var provider = CreateProvider(keyId: null);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            provider
                .UnwrapAsync(
                    wrappedKey,
                    null!,
                    "root-context"u8.ToArray(),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task UnwrapAsync_WithAccessFails_Propagates()
    {
        var failure = new RequestFailedException(403, "The directory has not consented.");
        var wrappedKey = CreateWrappedKey(TenantKeyVersionId, RandomNumberGenerator.GetBytes(384));
        SetupUnwrapClient(TenantKeyVersionId, Access);
        SetupUnwrapKeyThrows(failure);
        var provider = CreateProvider(keyId: null);

        var exception = await Assert.ThrowsAsync<RequestFailedException>(() =>
            provider
                .UnwrapAsync(
                    wrappedKey,
                    Access,
                    "root-context"u8.ToArray(),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task UnwrapAsync_WithAccessRejectedByTheService_ThrowsCryptographicException()
    {
        var failure = new RequestFailedException(400, "x");
        var wrappedKey = CreateWrappedKey(TenantKeyVersionId, RandomNumberGenerator.GetBytes(384));
        SetupUnwrapClient(TenantKeyVersionId, Access);
        SetupUnwrapKeyThrows(failure);
        var provider = CreateProvider(keyId: null);

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .UnwrapAsync(
                    wrappedKey,
                    Access,
                    "root-context"u8.ToArray(),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public async Task UnwrapAsync_WithAccessIsCanceled_Propagates()
    {
        var failure = new OperationCanceledException();
        var wrappedKey = CreateWrappedKey(TenantKeyVersionId, RandomNumberGenerator.GetBytes(384));
        SetupUnwrapClient(TenantKeyVersionId, Access);
        SetupUnwrapKeyThrows(failure);
        var provider = CreateProvider(keyId: null);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider
                .UnwrapAsync(
                    wrappedKey,
                    Access,
                    "root-context"u8.ToArray(),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    #endregion
}
