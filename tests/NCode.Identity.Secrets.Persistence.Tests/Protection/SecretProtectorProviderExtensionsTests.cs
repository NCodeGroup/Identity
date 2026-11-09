// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class SecretProtectorProviderExtensionsTests : BaseTests
{
    private const string PublicValue = "public-value";
    private const string RemoteValue = "aws-kms:remote-key";
    private const string FirstPrivateValue = "private-value-one";
    private const string SecondPrivateValue = "private-value-two";

    private static SecretOwner Owner { get; } = SecretOwner.ForTenant("tenant-one");

    private readonly Mock<ISecretProtectorProvider> _provider;
    private readonly Mock<ISecretProtector> _protector;

    public SecretProtectorProviderExtensionsTests()
    {
        _provider = CreateStrictMock<ISecretProtectorProvider>();
        _protector = CreateStrictMock<ISecretProtector>();
    }

    private static PersistedSecret CreateSecret(string secretEncoding, string encodedValue) =>
        new PersistedSecretBuilder
        {
            SecretId = Guid.NewGuid().ToString("N"),
            ExpiresWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch,
            SecretEncoding = secretEncoding,
            EncodedValue = encodedValue,
        }.Build();

    // The setup matches only the protected values that the predicate accepts, so any other collection fails the test.
    private void SetupGetProtector(Func<IReadOnlyCollection<string>, bool> predicate) =>
        _provider
            .Setup(x =>
                x.GetProtectorAsync(
                    Owner,
                    It.Is<IReadOnlyCollection<string>>(values => predicate(values)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<ISecretProtector>(_protector.Object))
            .Verifiable();

    #region GetProtectorForSecretsAsync Tests

    [Fact]
    public async Task GetProtectorForSecretsAsync_MixedSecrets_PassesOnlyPrivateValues()
    {
        PersistedSecret[] secrets =
        [
            CreateSecret(SecretEncodings.Pkcs8, FirstPrivateValue),
            CreateSecret(SecretEncodings.Spki, PublicValue),
            CreateSecret(SecretEncodings.SymmetricRaw, SecondPrivateValue),
        ];
        SetupGetProtector(values => values.SequenceEqual([FirstPrivateValue, SecondPrivateValue]));

        var result = await _provider.Object.GetProtectorForSecretsAsync(
            Owner,
            secrets,
            CancellationToken.None
        );

        Assert.Same(_protector.Object, result);
    }

    [Fact]
    public async Task GetProtectorForSecretsAsync_OnlyPublicSecrets_PassesEmpty()
    {
        PersistedSecret[] secrets =
        [
            CreateSecret(SecretEncodings.Spki, PublicValue),
            CreateSecret(SecretEncodings.X509, PublicValue),
        ];
        SetupGetProtector(values => values.Count == 0);

        var result = await _provider.Object.GetProtectorForSecretsAsync(
            Owner,
            secrets,
            CancellationToken.None
        );

        Assert.Same(_protector.Object, result);
    }

    [Fact]
    public async Task GetProtectorForSecretsAsync_RemoteSecrets_NeverPassesTheirValues()
    {
        PersistedSecret[] secrets =
        [
            CreateSecret(SecretEncodings.Remote, RemoteValue),
            CreateSecret(SecretEncodings.Pkcs8, FirstPrivateValue),
        ];
        SetupGetProtector(values => values.SequenceEqual([FirstPrivateValue]));

        var result = await _provider.Object.GetProtectorForSecretsAsync(
            Owner,
            secrets,
            CancellationToken.None
        );

        Assert.Same(_protector.Object, result);
    }

    [Fact]
    public async Task GetProtectorForSecretsAsync_OnlyRemoteSecrets_PassesEmpty()
    {
        PersistedSecret[] secrets = [CreateSecret(SecretEncodings.Remote, RemoteValue)];
        SetupGetProtector(values => values.Count == 0);

        var result = await _provider.Object.GetProtectorForSecretsAsync(
            Owner,
            secrets,
            CancellationToken.None
        );

        Assert.Same(_protector.Object, result);
    }

    [Fact]
    public async Task GetProtectorForSecretsAsync_NullSecrets_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _provider.Object.GetProtectorForSecretsAsync(Owner, null!, CancellationToken.None)
        );
    }

    #endregion
}
