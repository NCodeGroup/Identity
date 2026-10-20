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

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Algorithms;

public class AlgorithmCollectionExtensionsTests : BaseTests
{
    private ServiceProvider ServiceProvider { get; }
    private IAlgorithmCollection AlgorithmCollection { get; }

    public AlgorithmCollectionExtensionsTests()
    {
        // The real registered algorithms are the source of truth the rule reads.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        AlgorithmCollection = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
    }

    private string? Validate(Type keyType, int keySizeBits, string? use, string algorithm) =>
        AlgorithmCollection.ValidateKeyAlgorithm(
            new KeyAlgorithmDeclaration
            {
                KeyType = keyType,
                KeySizeBits = keySizeBits,
                Use = use,
                Algorithm = algorithm,
            }
        );

    #region ValidateKeyAlgorithm Tests

    [Theory]
    [InlineData(null)]
    [InlineData("sig")]
    public void ValidateKeyAlgorithm_WhenSignatureAlgorithmMatchesKey_ReturnsNull(string? use)
    {
        var error = Validate(typeof(RsaSecretKey), 2048, use, "RS256");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeyManagementAlgorithmMatchesKey_ReturnsNull()
    {
        var error = Validate(typeof(RsaSecretKey), 2048, "enc", "RSA-OAEP-256");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenAlgorithmIsUnknown_ReturnsError()
    {
        var error = Validate(typeof(RsaSecretKey), 2048, null, "XX999");

        Assert.NotNull(error);
        Assert.Contains("'XX999'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenUseContradictsAlgorithm_ReturnsError()
    {
        var error = Validate(typeof(RsaSecretKey), 2048, SecretKeyUses.Encryption, "RS256");

        Assert.NotNull(error);
        Assert.Contains("'sig'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeyTypeIsIncompatible_ReturnsError()
    {
        var error = Validate(typeof(EccSecretKey), 256, null, "RS256");

        Assert.NotNull(error);
        Assert.Contains("type", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateKeyAlgorithm_WhenKeySizeIsIllegal_ReturnsError()
    {
        var error = Validate(typeof(EccSecretKey), 384, null, "ES256");

        Assert.NotNull(error);
        Assert.Contains("'384'", error, StringComparison.Ordinal);
    }

    #endregion
}
