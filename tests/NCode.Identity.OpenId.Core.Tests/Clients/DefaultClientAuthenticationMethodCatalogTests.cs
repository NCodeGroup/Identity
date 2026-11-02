// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Clients;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultClientAuthenticationMethodCatalogTests : BaseTests
{
    private static DefaultClientAuthenticationMethodCatalog CreateCatalog(
        params IEnumerable<ClientAuthenticationMethodDescriptor>[] dataSources
    ) =>
        new([
            .. dataSources.Select(
                items => new StaticDataSource<ClientAuthenticationMethodDescriptor>(items)
            ),
        ]);

    #region MethodNames Tests

    [Fact]
    public void MethodNames_KeepsTheRegisteredOrder()
    {
        var catalog = CreateCatalog(
            [
                new ClientAuthenticationMethodDescriptor { Name = "zeta" },
                new ClientAuthenticationMethodDescriptor { Name = "alpha" },
            ],
            [new ClientAuthenticationMethodDescriptor { Name = "mu" }]
        );

        Assert.Equal(["zeta", "alpha", "mu"], catalog.MethodNames);
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_WhenTheMethodIsRegistered_ReturnsItsDescriptor()
    {
        var descriptor = new ClientAuthenticationMethodDescriptor
        {
            Name = "client_secret_basic",
            RequiresClientSecret = true,
        };
        var catalog = CreateCatalog(
            [new ClientAuthenticationMethodDescriptor { Name = "none" }],
            [descriptor]
        );

        var actual = catalog.TryGet("client_secret_basic", out var found);

        Assert.True(actual);
        Assert.Same(descriptor, found);
    }

    [Fact]
    public void TryGet_WhenTheMethodIsNotRegistered_ReturnsFalse()
    {
        var catalog = CreateCatalog([new ClientAuthenticationMethodDescriptor { Name = "none" }]);

        var actual = catalog.TryGet("client_secret_basic", out var found);

        Assert.False(actual);
        Assert.Null(found);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenTwoDescriptorsShareAName_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog(
                [new ClientAuthenticationMethodDescriptor { Name = "none" }],
                [new ClientAuthenticationMethodDescriptor { Name = "none" }]
            )
        );

        Assert.Equal(
            "More than one client-authentication method is named 'none'.",
            exception.Message
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("client secret")]
    public void Constructor_WhenANameIsEmptyOrHasWhitespace_Throws(string name)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog([new ClientAuthenticationMethodDescriptor { Name = name }])
        );

        Assert.Equal(
            "A client-authentication method descriptor must have a name that is not empty and contains no whitespace.",
            exception.Message
        );
    }

    #endregion

    private sealed class StaticDataSource<T>(IEnumerable<T> items) : ICollectionDataSource<T>
    {
        public IEnumerable<T> Collection { get; } = [.. items];

        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }
}
