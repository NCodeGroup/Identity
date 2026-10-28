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
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class DefaultRootKeyProviderCatalogTests : IDisposable
{
    private readonly MockRepository _mocks = new(MockBehavior.Strict);

    public void Dispose()
    {
        _mocks.Verify();
    }

    private Mock<IKeyWrapper> CreateProvider(string providerName)
    {
        var provider = _mocks.Create<IKeyWrapper>();
        provider.Setup(x => x.Name).Returns(providerName).Verifiable();
        return provider;
    }

    private static DefaultRootKeyProviderCatalog CreateCatalog(
        IEnumerable<IKeyWrapper> providers,
        string? rootKeyProvider = null
    ) =>
        new(
            providers,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RootKeyProvider = rootKeyProvider }
            )
        );

    #region Constructor Tests

    [Fact]
    public void Constructor_DuplicateNames_Throws()
    {
        var first = CreateProvider("duplicate");
        var second = CreateProvider("duplicate");
        var other = CreateProvider("other");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog([first.Object, second.Object, other.Object])
        );

        Assert.Contains("'duplicate'", exception.Message);
        Assert.DoesNotContain("'other'", exception.Message);
    }

    #endregion

    #region Get Tests

    [Fact]
    public void Get_Registered_ReturnsProvider()
    {
        var ephemeral = CreateProvider(KeyWrapperNames.Ephemeral);
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog([ephemeral.Object, local.Object]);

        var result = catalog.Get(KeyWrapperNames.Local);

        Assert.Same(local.Object, result);
    }

    [Fact]
    public void Get_Unknown_ThrowsListingNames()
    {
        var ephemeral = CreateProvider(KeyWrapperNames.Ephemeral);
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog([ephemeral.Object, local.Object]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.Get(KeyWrapperNames.AwsKms)
        );

        Assert.Contains($"'{KeyWrapperNames.AwsKms}'", exception.Message);
        Assert.Contains($"'{KeyWrapperNames.Ephemeral}'", exception.Message);
        Assert.Contains($"'{KeyWrapperNames.Local}'", exception.Message);
    }

    #endregion

    #region GetConfigured Tests

    [Fact]
    public void GetConfigured_Unset_ReturnsEphemeral()
    {
        var ephemeral = CreateProvider(KeyWrapperNames.Ephemeral);
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog([ephemeral.Object, local.Object]);

        var result = catalog.GetConfigured();

        Assert.Same(ephemeral.Object, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void GetConfigured_Blank_ReturnsEphemeral(string rootKeyProvider)
    {
        var ephemeral = CreateProvider(KeyWrapperNames.Ephemeral);
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog(
            [ephemeral.Object, local.Object],
            rootKeyProvider: rootKeyProvider
        );

        var result = catalog.GetConfigured();

        Assert.Same(ephemeral.Object, result);
    }

    [Fact]
    public void GetConfigured_Set_ReturnsNamedProvider()
    {
        var ephemeral = CreateProvider(KeyWrapperNames.Ephemeral);
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog(
            [ephemeral.Object, local.Object],
            rootKeyProvider: KeyWrapperNames.Local
        );

        var result = catalog.GetConfigured();

        Assert.Same(local.Object, result);
    }

    #endregion
}
