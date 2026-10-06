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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets;
using Xunit;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

public sealed class DefaultSecretValidatorTests : IAsyncLifetime
{
    private ServiceProvider ServiceProvider { get; }
    private DefaultSecretValidator Validator { get; }

    public DefaultSecretValidatorTests()
    {
        // Resolve the real algorithm collection so the test asserts against the exact set of algorithms
        // the server supports — the same source of truth the validator reads at runtime.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary();
        serviceCollection.AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        Validator = new DefaultSecretValidator(
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>()
        );
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
    }

    private static CreateSecretRequest CreateRequest(
        string secretType,
        int keySizeBits,
        string? use = null,
        string? algorithm = null
    ) =>
        new()
        {
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Use = use,
            Algorithm = algorithm,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    #region SecretType Tests

    [Theory]
    [InlineData("symmetric", 256)]
    [InlineData("rsa", 2048)]
    [InlineData("ecc", 256)]
    public void ValidateCreate_WhenKnownSecretTypeAndNoUseOrAlgorithm_ReturnsNull(
        string secretType,
        int keySizeBits
    )
    {
        var error = Validator.ValidateCreate(CreateRequest(secretType, keySizeBits));

        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("oct")]
    [InlineData("RSA")]
    [InlineData("unknown")]
    public void ValidateCreate_WhenUnknownSecretType_Returns400(string secretType)
    {
        var error = Validator.ValidateCreate(CreateRequest(secretType, 256));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    #endregion

    #region KeySize Tests

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    [InlineData(100)]
    [InlineData(-128)]
    public void ValidateCreate_WhenSymmetricKeySizeInvalid_Returns400(int keySizeBits)
    {
        var error = Validator.ValidateCreate(CreateRequest("symmetric", keySizeBits));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(2047)]
    [InlineData(32768)]
    public void ValidateCreate_WhenRsaKeySizeInvalid_Returns400(int keySizeBits)
    {
        var error = Validator.ValidateCreate(CreateRequest("rsa", keySizeBits));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(3072)]
    [InlineData(4096)]
    public void ValidateCreate_WhenRsaKeySizeValid_ReturnsNull(int keySizeBits)
    {
        var error = Validator.ValidateCreate(CreateRequest("rsa", keySizeBits));

        Assert.Null(error);
    }

    [Theory]
    [InlineData(255)]
    [InlineData(512)]
    [InlineData(520)]
    public void ValidateCreate_WhenEccKeySizeInvalid_Returns400(int keySizeBits)
    {
        var error = Validator.ValidateCreate(CreateRequest("ecc", keySizeBits));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(384)]
    [InlineData(521)]
    public void ValidateCreate_WhenEccKeySizeValid_ReturnsNull(int keySizeBits)
    {
        var error = Validator.ValidateCreate(CreateRequest("ecc", keySizeBits));

        Assert.Null(error);
    }

    #endregion

    #region Use Tests

    [Theory]
    [InlineData("sig")]
    [InlineData("enc")]
    public void ValidateCreate_WhenKnownUse_ReturnsNull(string use)
    {
        var error = Validator.ValidateCreate(CreateRequest("symmetric", 256, use));

        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("signature")]
    [InlineData("SIG")]
    public void ValidateCreate_WhenUnknownUse_Returns400(string use)
    {
        var error = Validator.ValidateCreate(CreateRequest("symmetric", 256, use));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    #endregion

    #region Algorithm Compatibility Tests

    [Theory]
    [InlineData("rsa", 2048, "sig", "RS256")]
    [InlineData("rsa", 2048, "sig", "PS512")]
    [InlineData("ecc", 256, "sig", "ES256")]
    [InlineData("symmetric", 256, "sig", "HS256")]
    [InlineData("rsa", 2048, "enc", "RSA-OAEP-256")]
    [InlineData("ecc", 256, "enc", "ECDH-ES")]
    [InlineData("symmetric", 256, "enc", "A256KW")]
    public void ValidateCreate_WhenAlgorithmMatchesUseAndType_ReturnsNull(
        string secretType,
        int keySizeBits,
        string use,
        string algorithm
    )
    {
        var error = Validator.ValidateCreate(
            CreateRequest(secretType, keySizeBits, use, algorithm)
        );

        Assert.Null(error);
    }

    [Fact]
    public void ValidateCreate_WhenSignatureAlgorithmPairedWithEncryptionUse_Returns400()
    {
        var error = Validator.ValidateCreate(CreateRequest("rsa", 2048, "enc", "RS256"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateCreate_WhenEncryptionAlgorithmPairedWithSignatureUse_Returns400()
    {
        var error = Validator.ValidateCreate(CreateRequest("rsa", 2048, "sig", "RSA-OAEP"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateCreate_WhenAlgorithmRequiresDifferentSecretType_Returns400()
    {
        var error = Validator.ValidateCreate(CreateRequest("ecc", 256, "sig", "RS256"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateCreate_WhenAlgorithmIncompatibleWithTypeAndNoUse_Returns400()
    {
        var error = Validator.ValidateCreate(
            CreateRequest("symmetric", 256, use: null, algorithm: "RS256")
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateCreate_WhenKeySizeIllegalForAlgorithm_Returns400()
    {
        // RS256 requires a key of at least 2048 bits (RFC 7518); 1024 is a multiple of 8 but too small.
        var error = Validator.ValidateCreate(CreateRequest("rsa", 1024, "sig", "RS256"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public void ValidateCreate_WhenAlgorithmNotRegistered_Returns400()
    {
        // An algorithm the server does not support can never be used, so creation is rejected.
        var error = Validator.ValidateCreate(
            CreateRequest("rsa", 2048, "sig", algorithm: "CUSTOM-ALG")
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    #endregion
}
