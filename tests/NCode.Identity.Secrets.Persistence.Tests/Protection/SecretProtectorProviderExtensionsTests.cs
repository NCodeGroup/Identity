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

using Moq;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class SecretProtectorProviderExtensionsTests : IDisposable
{
    private const string PublicValue = "public-value";
    private const string FirstPrivateValue = "private-value-one";
    private const string SecondPrivateValue = "private-value-two";

    private static readonly SecretOwner Owner = SecretOwner.ForTenant("tenant-one");

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<ISecretProtectorProvider> _provider;
    private readonly Mock<ISecretProtector> _protector;

    public SecretProtectorProviderExtensionsTests()
    {
        _provider = _mocks.Create<ISecretProtectorProvider>();
        _protector = _mocks.Create<ISecretProtector>();
    }

    public void Dispose()
    {
        _mocks.Verify();
    }

    private static PersistedSecret CreateSecret(string secretEncoding, string encodedValue) =>
        new()
        {
            SecretId = Guid.NewGuid().ToString("N"),
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch,
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = secretEncoding,
            KeySizeBits = 256,
            EncodedValue = encodedValue,
        };

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
    public async Task GetProtectorForSecretsAsync_NullSecrets_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _provider.Object.GetProtectorForSecretsAsync(Owner, null!, CancellationToken.None)
        );
    }

    #endregion
}
