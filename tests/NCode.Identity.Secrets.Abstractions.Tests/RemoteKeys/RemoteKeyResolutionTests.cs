// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;

namespace NCode.Identity.Secrets.RemoteKeys;

public class RemoteKeyResolutionTests : BaseTests
{
    private static RemoteSigningAlgorithm CreateSigningRow() =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RS256",
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            SigningAlgorithm = "RSASSA_PKCS1_V1_5_SHA_256",
        };

    private Mock<IRemoteKeyProvider> CreateProvider()
    {
        var provider = CreateStrictMock<IRemoteKeyProvider>();
        provider.Setup(x => x.Name).Returns(RemoteKeyProviderNames.AwsKms).Verifiable();
        return provider;
    }

    #region Bind Tests

    [Fact]
    public void Bind_WithAKeyId_BindsTheProviderReferenceAndRow()
    {
        var provider = CreateProvider();
        var row = CreateSigningRow();
        var resolution = new RemoteKeyResolution { Provider = provider.Object, Algorithm = row };

        var binding = resolution.Bind("key-1");

        Assert.Same(provider.Object, binding.Provider);
        Assert.Equal(
            new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, "key-1"),
            binding.Reference
        );
        Assert.Same(row, binding.Algorithm);
    }

    [Fact]
    public void Bind_WithAnEmptyKeyId_Throws()
    {
        var provider = CreateProvider();
        var resolution = new RemoteKeyResolution
        {
            Provider = provider.Object,
            Algorithm = CreateSigningRow(),
        };

        Assert.Throws<ArgumentException>(() => resolution.Bind(string.Empty));
    }

    #endregion
}
