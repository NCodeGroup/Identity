// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultClientSecretIssuerTests : BaseTests
{
    private const string KeyedMethod = "keyed_method";
    private const string ComparedMethod = "compared_method";

    private Mock<IClientAuthenticationMethodCatalog> MockCatalog { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private ISecretProtector Protector { get; }

    public DefaultClientSecretIssuerTests()
    {
        MockCatalog = CreateStrictMock<IClientAuthenticationMethodCatalog>();
        MockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        Protector = CreateStrictMock<ISecretProtector>().Object;
    }

    #region Helpers

    private DefaultClientSecretIssuer CreateIssuer() =>
        new(MockCatalog.Object, MockSecretGenerator.Object);

    private void SetupMethod(string method, bool usesClientSecretAsKey)
    {
        ClientAuthenticationMethodDescriptor? descriptor = new()
        {
            Name = method,
            RequiresClientSecret = true,
            UsesClientSecretAsKey = usesClientSecretAsKey,
        };
        MockCatalog.Setup(x => x.TryGet(method, out descriptor)).Returns(true).Verifiable();
    }

    private void SetupUnregisteredMethod(string method)
    {
        ClientAuthenticationMethodDescriptor? descriptor = null;
        MockCatalog.Setup(x => x.TryGet(method, out descriptor)).Returns(false).Verifiable();
    }

    private static GenerateSecretRequest CreateGenerateRequest() =>
        new()
        {
            SecretId = "secret-1",
            SecretType = SecretTypes.Symmetric,
            KeySizeBits = 256,
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.MaxValue,
        };

    private static GeneratedSecret CreateGeneratedSecret(string secretEncoding) =>
        new()
        {
            Secret = CreatePersistedSecret(SecretTypes.Symmetric, secretEncoding),
            SecretMaterial = "the-client-secret",
        };

    private static PersistedSecret CreatePersistedSecret(
        string secretType,
        string secretEncoding
    ) =>
        new()
        {
            SecretId = "secret-1",
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.MaxValue,
            SecretType = secretType,
            SecretEncoding = secretEncoding,
            KeySizeBits = 256,
            EncodedValue = "nce1.value",
        };

    #endregion

    #region GenerateClientSecret Tests

    [Fact]
    public void GenerateClientSecret_WhenTheMethodUsesTheSecretAsAKey_GeneratesAReversibleSecret()
    {
        SetupMethod(KeyedMethod, usesClientSecretAsKey: true);
        var request = CreateGenerateRequest();
        var expected = CreateGeneratedSecret(SecretEncodings.SymmetricRaw);
        MockSecretGenerator
            .Setup(x => x.GenerateReversibleSharedSecret(Protector, request))
            .Returns(expected)
            .Verifiable();

        var actual = CreateIssuer().GenerateClientSecret(Protector, KeyedMethod, request);

        Assert.Same(expected, actual);
    }

    [Fact]
    public void GenerateClientSecret_WhenTheMethodComparesTheSecret_GeneratesAOneWaySecret()
    {
        SetupMethod(ComparedMethod, usesClientSecretAsKey: false);
        var request = CreateGenerateRequest();
        var expected = CreateGeneratedSecret(SecretEncodings.Verifier);
        MockSecretGenerator
            .Setup(x => x.GenerateOneWaySharedSecret(Protector, request))
            .Returns(expected)
            .Verifiable();

        var actual = CreateIssuer().GenerateClientSecret(Protector, ComparedMethod, request);

        Assert.Same(expected, actual);
    }

    [Fact]
    public void GenerateClientSecret_WhenTheMethodIsNotRegistered_GeneratesAOneWaySecret()
    {
        // A method the server cannot name keys nothing, so its secret fails closed as a verifier.
        SetupUnregisteredMethod("unregistered_method");
        var request = CreateGenerateRequest();
        var expected = CreateGeneratedSecret(SecretEncodings.Verifier);
        MockSecretGenerator
            .Setup(x => x.GenerateOneWaySharedSecret(Protector, request))
            .Returns(expected)
            .Verifiable();

        var actual = CreateIssuer().GenerateClientSecret(Protector, "unregistered_method", request);

        Assert.Same(expected, actual);
    }

    [Fact]
    public void GenerateClientSecret_WhenTheMethodIsUnknown_GeneratesAOneWaySecret()
    {
        var request = CreateGenerateRequest();
        var expected = CreateGeneratedSecret(SecretEncodings.Verifier);
        MockSecretGenerator
            .Setup(x => x.GenerateOneWaySharedSecret(Protector, request))
            .Returns(expected)
            .Verifiable();

        var actual = CreateIssuer().GenerateClientSecret(Protector, null, request);

        Assert.Same(expected, actual);
    }

    #endregion

    #region CanAuthenticate Tests

    [Theory]
    [InlineData(KeyedMethod, true, SecretEncodings.SymmetricRaw, true)]
    [InlineData(KeyedMethod, true, SecretEncodings.Verifier, false)]
    [InlineData(ComparedMethod, false, SecretEncodings.SymmetricRaw, true)]
    [InlineData(ComparedMethod, false, SecretEncodings.Verifier, true)]
    public void CanAuthenticate_WhenTheSecretIsShared_FollowsTheMethodsForm(
        string method,
        bool usesClientSecretAsKey,
        string secretEncoding,
        bool expected
    )
    {
        SetupMethod(method, usesClientSecretAsKey);
        var secret = CreatePersistedSecret(SecretTypes.Symmetric, secretEncoding);

        var actual = CreateIssuer().CanAuthenticate(secret, method);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanAuthenticate_WhenTheSecretIsAKeyPair_ReturnsFalse()
    {
        var secret = CreatePersistedSecret(SecretTypes.Rsa, SecretEncodings.Pkcs8);

        var actual = CreateIssuer().CanAuthenticate(secret, ComparedMethod);

        Assert.False(actual);
    }

    #endregion
}
