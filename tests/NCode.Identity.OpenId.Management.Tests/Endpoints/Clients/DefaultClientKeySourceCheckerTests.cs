// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public sealed class DefaultClientKeySourceCheckerTests
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<ISecretKeyTypeCatalog> MockSecretKeyTypeCatalog { get; }

    private DefaultClientKeySourceChecker Checker { get; }

    public DefaultClientKeySourceCheckerTests()
    {
        MockSecretKeyTypeCatalog = MockRepository.Create<ISecretKeyTypeCatalog>();
        Checker = new DefaultClientKeySourceChecker(MockSecretKeyTypeCatalog.Object);
    }

    private static PersistedSecret CreateSecret(string secretType) =>
        new PersistedSecretBuilder
        {
            RetiresWhen = DateTimeOffset.MaxValue,
            ExpiresWhen = DateTimeOffset.MaxValue,
            SecretType = secretType,
            SecretEncoding = "base64",
            Origin = "imported",
            EncodedValue = "value",
        }.Build();

    private void SetupKeyType(string secretType, bool? isAsymmetric)
    {
        if (isAsymmetric is null)
        {
            ISecretKeyTypeDescriptor? missing = null;
            MockSecretKeyTypeCatalog
                .Setup(x => x.TryGet(secretType, out missing))
                .Returns(false)
                .Verifiable();
            return;
        }

        var mockDescriptor = MockRepository.Create<ISecretKeyTypeDescriptor>();
        mockDescriptor.Setup(x => x.IsAsymmetric).Returns(isAsymmetric.Value).Verifiable();
        ISecretKeyTypeDescriptor? descriptor = mockDescriptor.Object;
        MockSecretKeyTypeCatalog
            .Setup(x => x.TryGet(secretType, out descriptor))
            .Returns(true)
            .Verifiable();
    }

    #region HasJwksUri Tests

    [Fact]
    public void HasJwksUri_WhenTheSettingsAreNotAnObject_ReturnsFalse()
    {
        Assert.False(Checker.HasJwksUri(JsonElement.Parse("[]")));
    }

    [Fact]
    public void HasJwksUri_WhenTheJwksUriIsMissing_ReturnsFalse()
    {
        Assert.False(Checker.HasJwksUri(JsonElement.Parse("{}")));
    }

    [Fact]
    public void HasJwksUri_WhenTheJwksUriIsEmpty_ReturnsFalse()
    {
        Assert.False(Checker.HasJwksUri(JsonElement.Parse("""{"jwks_uri":""}""")));
    }

    [Fact]
    public void HasJwksUri_WhenTheJwksUriIsSet_ReturnsTrue()
    {
        Assert.True(
            Checker.HasJwksUri(JsonElement.Parse("""{"jwks_uri":"https://client/jwks"}"""))
        );
    }

    #endregion

    #region IsPublicKeyType Tests

    [Fact]
    public void IsPublicKeyType_WhenTheTypeIsAsymmetric_ReturnsTrue()
    {
        SetupKeyType("rsa", isAsymmetric: true);

        Assert.True(Checker.IsPublicKeyType("rsa"));

        MockRepository.Verify();
    }

    [Fact]
    public void IsPublicKeyType_WhenTheTypeIsSymmetric_ReturnsFalse()
    {
        SetupKeyType("oct", isAsymmetric: false);

        Assert.False(Checker.IsPublicKeyType("oct"));

        MockRepository.Verify();
    }

    [Fact]
    public void IsPublicKeyType_WhenTheTypeIsUnknown_ReturnsFalse()
    {
        SetupKeyType("unknown", isAsymmetric: null);

        Assert.False(Checker.IsPublicKeyType("unknown"));

        MockRepository.Verify();
    }

    #endregion

    #region HasPublicKeys Tests

    [Fact]
    public void HasPublicKeys_WhenASecretIsAPublicKeyType_ReturnsTrue()
    {
        SetupKeyType("oct", isAsymmetric: false);
        SetupKeyType("rsa", isAsymmetric: true);

        Assert.True(Checker.HasPublicKeys([CreateSecret("oct"), CreateSecret("rsa")]));

        MockRepository.Verify();
    }

    [Fact]
    public void HasPublicKeys_WhenNoSecretIsAPublicKeyType_ReturnsFalse()
    {
        SetupKeyType("oct", isAsymmetric: false);

        Assert.False(Checker.HasPublicKeys([CreateSecret("oct")]));

        MockRepository.Verify();
    }

    #endregion
}
